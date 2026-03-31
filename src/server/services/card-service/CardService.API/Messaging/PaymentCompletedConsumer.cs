using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using CardService.Infrastructure.Persistence.Sql;
using Shared.Contracts.Events.Payment;

namespace CardService.API.Messaging;

public class PaymentCompletedConsumer(ICardRepository cards, CardDbContext db, ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        var paymentId = context.Message.PaymentId;
        var cardId = context.Message.CardId;
        var amount = context.Message.Amount;

        logger.LogInformation("Payment completed received: PaymentId={PaymentId}, CardId={CardId}, Amount={Amount}",
            paymentId, cardId, amount);

        try
        {
            if (await db.CardTransactions.AnyAsync(x => x.Description == $"PaymentService:{paymentId}"))
            {
                logger.LogInformation("Payment {PaymentId} already processed (idempotency check)", paymentId);
                return;
            }

            var card = await cards.GetByIdAsync(cardId);
            if (card == null)
            {
                logger.LogError("Card {CardId} not found for payment {PaymentId}", cardId, paymentId);
                throw new InvalidOperationException($"Card {cardId} not found");
            }

            var oldBalance = card.OutstandingBalance;
            card.OutstandingBalance = Math.Max(card.OutstandingBalance - amount, 0);
            card.UpdatedAtUtc = DateTime.UtcNow;

            logger.LogInformation("Card {CardId}: Balance updated from {OldBalance} to {NewBalance}",
                card.Id, oldBalance, card.OutstandingBalance);

            db.CardTransactions.Add(new CardTransaction
            {
                Id = Guid.NewGuid(), CardId = card.Id, UserId = card.UserId,
                Type = TransactionType.Payment, Amount = amount,
                Description = $"PaymentService:{paymentId}", DateUtc = DateTime.UtcNow
            });

            await cards.UpdateAsync(card);
            await db.SaveChangesAsync(context.CancellationToken);
            logger.LogInformation("Card balance updated successfully: CardId={CardId}", card.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process payment {PaymentId} for card {CardId}", paymentId, cardId);
            throw;
        }
    }
}
