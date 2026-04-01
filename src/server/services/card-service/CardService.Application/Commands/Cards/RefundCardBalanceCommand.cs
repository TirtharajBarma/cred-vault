using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public record RefundCardBalanceCommand(
    Guid CardId,
    Guid UserId,
    Guid PaymentId,
    decimal Amount
) : IRequest<RefundCardBalanceResult>;

public record RefundCardBalanceResult(
    bool Success,
    string? Message,
    decimal? NewBalance = null
);

public class RefundCardBalanceCommandHandler(
    ICardRepository cardRepository,
    ICardDbContextAccessor dbContext,
    ILogger<RefundCardBalanceCommandHandler> logger
) : IRequestHandler<RefundCardBalanceCommand, RefundCardBalanceResult>
{
    public async Task<RefundCardBalanceResult> Handle(RefundCardBalanceCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing refund: CardId={CardId}, PaymentId={PaymentId}, Amount={Amount}",
            request.CardId, request.PaymentId, request.Amount);

        try
        {
            var existingRefund = await dbContext.AnyAsync<CardTransaction>(
                x => x.Description == $"Refund:PaymentService:{request.PaymentId}", cancellationToken);

            if (existingRefund)
            {
                logger.LogWarning("Refund already processed for PaymentId={PaymentId}", request.PaymentId);
                return new RefundCardBalanceResult(false, "Refund already processed");
            }

            var card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
            if (card == null)
            {
                logger.LogError("Card {CardId} not found for refund", request.CardId);
                return new RefundCardBalanceResult(false, "Card not found");
            }

            if (card.UserId != request.UserId)
            {
                logger.LogWarning("IDOR attempt: Card {CardId} belongs to {CardUserId}, not {RequestUserId}",
                    request.CardId, card.UserId, request.UserId);
                return new RefundCardBalanceResult(false, "Card does not belong to user");
            }

            var oldBalance = card.OutstandingBalance;
            card.OutstandingBalance += request.Amount;

            if (card.OutstandingBalance > card.CreditLimit)
            {
                card.OutstandingBalance = card.CreditLimit;
            }

            card.UpdatedAtUtc = DateTime.UtcNow;

            dbContext.Add(new CardTransaction
            {
                Id = Guid.NewGuid(),
                CardId = card.Id,
                UserId = card.UserId,
                Type = TransactionType.Refund,
                Amount = request.Amount,
                Description = $"Refund:PaymentService:{request.PaymentId}",
                DateUtc = DateTime.UtcNow
            });

            await cardRepository.UpdateAsync(card);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Refund successful: CardId={CardId}, OldBalance={OldBalance}, NewBalance={NewBalance}",
                card.Id, oldBalance, card.OutstandingBalance);

            return new RefundCardBalanceResult(true, "Refund successful", card.OutstandingBalance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process refund for PaymentId={PaymentId}", request.PaymentId);
            return new RefundCardBalanceResult(false, $"Refund failed: {ex.Message}");
        }
    }
}
