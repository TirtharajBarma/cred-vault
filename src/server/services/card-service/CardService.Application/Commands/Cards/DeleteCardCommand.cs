using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs;
using MediatR;
using CardService.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Exceptions;

namespace CardService.Application.Commands.Cards;

public sealed record DeleteCardCommand(Guid UserId, Guid CardId) : IRequest<OperationResult>;

public sealed class DeleteCardCommandHandler(
    ICardRepository cardRepository,
    IBillingServiceClient billingServiceClient,
    ILogger<DeleteCardCommandHandler> logger)
    : IRequestHandler<DeleteCardCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeleteCardCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card", request.CardId);
        }

        if (card.OutstandingBalance > 0)
        {
            throw new ForbiddenException("Cannot delete card with outstanding balance. Pay your bill first.");
        }

        var hasPendingBill = await billingServiceClient.HasPendingBillAsync(request.CardId, cancellationToken);
        if (hasPendingBill)
        {
            throw new ForbiddenException("Cannot delete card with a pending or overdue bill. Please pay or wait for the bill to be cleared.");
        }

        await cardRepository.DeleteAsync(card, cancellationToken);

        logger.LogInformation("Card {CardId} deleted by user {UserId}", request.CardId, request.UserId);

        return new OperationResult { Success = true, Message = "Card deleted successfully." };
    }
}
