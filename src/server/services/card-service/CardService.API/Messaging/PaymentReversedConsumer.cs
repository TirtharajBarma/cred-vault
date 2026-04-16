using CardService.Infrastructure.Persistence.Sql;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Payment;

namespace CardService.API.Messaging;

public class PaymentReversedConsumer(
    ILogger<PaymentReversedConsumer> logger,
    CardDbContext dbContext
) : IConsumer<IPaymentReversed>
{
    public async Task Consume(ConsumeContext<IPaymentReversed> context)
    {
        var message = context.Message;      // got the actual event data

        logger.LogInformation("PaymentReversed: PaymentId={PaymentId}, CardId={CardId}, Amount={Amount} - Restoring card balance", 
            message.PaymentId, message.CardId, message.Amount);

        try
        {
            var card = await dbContext.CreditCards      // find card
                .IgnoreQueryFilters()                   // ignore soft-deleted card [still fetches it]
                .FirstOrDefaultAsync(x => x.Id == message.CardId, context.CancellationToken);

            if (card is null)
            {
                logger.LogWarning("Card not found for reversal: CardId={CardId}", message.CardId);
                return;
            }

            card.OutstandingBalance += message.Amount;      // restore balance
            card.UpdatedAtUtc = DateTime.UtcNow;

            await dbContext.SaveChangesAsync(context.CancellationToken);    // save to db

            logger.LogInformation("Card balance restored: CardId={CardId}, NewOutstanding={Outstanding}", 
                message.CardId, card.OutstandingBalance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore card balance for CardId={CardId}", message.CardId);
            throw;
        }
    }
}
