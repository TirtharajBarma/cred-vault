using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using CardService.Infrastructure.Persistence.Sql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Events.Payment;

namespace CardService.API.Messaging;

/// <summary>
/// Consumes PaymentCompleted events from PaymentService via RabbitMQ.
/// Reduces the card's OutstandingBalance and records a Payment transaction.
/// </summary>
public class PaymentCompletedConsumer(
    ICardRepository cardRepository,
    CardDbContext db,
    ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        var msg = context.Message;
        logger.LogInformation("PaymentCompleted received: PaymentId={PaymentId} CardId={CardId}", msg.PaymentId, msg.CardId);

        // Idempotency — check if already processed
        var alreadyProcessed = await db.CardTransactions
            .AnyAsync(x => x.Description == $"PaymentService:{msg.PaymentId}");

        if (alreadyProcessed)
        {
            logger.LogInformation("Payment {PaymentId} already processed for card, skipping", msg.PaymentId);
            return;
        }

        var card = await cardRepository.GetByIdAsync(msg.CardId);
        if (card is null)
        {
            logger.LogWarning("Card {CardId} not found for PaymentCompleted event", msg.CardId);
            return;
        }

        var oldBalance = card.OutstandingBalance;
        logger.LogInformation("Card {CardId}: OldBalance={OldBalance}, PaymentAmount={Amount}", card.Id, oldBalance, msg.Amount);
        
        card.OutstandingBalance = Math.Max(card.OutstandingBalance - msg.Amount, 0);
        
        logger.LogInformation("Card {CardId}: NewBalance={NewBalance}", card.Id, card.OutstandingBalance);
        card.UpdatedAtUtc = DateTime.UtcNow;

        db.CardTransactions.Add(new CardTransaction
        {
            Id = Guid.NewGuid(),
            CardId = card.Id,
            UserId = card.UserId,
            Type = TransactionType.Payment,
            Amount = msg.Amount,
            Description = $"PaymentService:{msg.PaymentId}",
            DateUtc = DateTime.UtcNow
        });

        await cardRepository.UpdateAsync(card);
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Card {CardId} balance reduced by {Amount}.", card.Id, msg.Amount);
    }
}
