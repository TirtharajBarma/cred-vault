using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Exceptions;

namespace CardService.Application.Commands.Transactions;

public record AddCardTransactionCommand(
    Guid UserId, 
    Guid CardId, 
    TransactionType Type, 
    decimal Amount, 
    string? Description, 
    DateTime? DateUtc,
    bool IsAdmin
) : IRequest<ApiResponse<CardTransaction>>;

public sealed class AddCardTransactionCommandHandler(ICardRepository cardRepository, ILogger<AddCardTransactionCommandHandler> logger)
    : IRequestHandler<AddCardTransactionCommand, ApiResponse<CardTransaction>>
{
    public async Task<ApiResponse<CardTransaction>> Handle(AddCardTransactionCommand request, CancellationToken cancellationToken)
    {
        CreditCard? card;
        
        if (request.IsAdmin)
        {
            card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
        }
        else
        {
            card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);
        }

        if (card is null)
        {
            throw new NotFoundException("Card", request.CardId);
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException("Amount must be greater than 0.");
        }

        var dateUtc = request.DateUtc ?? DateTime.UtcNow;
        var isDuplicate = await cardRepository.HasDuplicateTransactionAsync(
            request.CardId, request.Type, request.Amount, request.Description ?? string.Empty, dateUtc, cancellationToken);
        if (isDuplicate)
        {
            throw new ConflictException("A duplicate transaction already exists.");
        }

        if (request.Type == TransactionType.Purchase)
        {
            if (card.OutstandingBalance + request.Amount > card.CreditLimit)
            {
                throw new ForbiddenException("Transaction declined: Insufficient credit limit.");
            }

            card.OutstandingBalance += request.Amount;
        }
        else
        {
            card.OutstandingBalance = Math.Max(card.OutstandingBalance - request.Amount, 0);
        }

        var txn = new CardTransaction
        {
            Id = Guid.NewGuid(),
            CardId = request.CardId,
            UserId = card.UserId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description ?? string.Empty,
            DateUtc = dateUtc
        };

        card.UpdatedAtUtc = DateTime.UtcNow;

        await cardRepository.AddTransactionAsync(txn, cancellationToken);
        await cardRepository.UpdateAsync(card, cancellationToken);

        logger.LogInformation("Added transaction {TransactionId} to Card {CardId}.", txn.Id, card.Id);

        return new ApiResponse<CardTransaction> 
        { 
            Success = true, 
            Message = "Transaction added.", 
            Data = txn 
        };
    }
}
