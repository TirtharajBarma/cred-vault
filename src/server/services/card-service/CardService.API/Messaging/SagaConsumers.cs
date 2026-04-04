using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using CardService.Infrastructure.Persistence.Sql;
using Shared.Contracts.Enums;
using Shared.Contracts.Events.Saga;

namespace CardService.API.Messaging;

public class CardDeductionSagaConsumer(
    ICardRepository cardRepository,
    CardDbContext dbContext,
    ILogger<CardDeductionSagaConsumer> logger
) : IConsumer<ICardDeductionRequested>
{
    public async Task Consume(ConsumeContext<ICardDeductionRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("CardDeductionSagaConsumer: CorrelationId={CorrelationId}, CardId={CardId}, Amount={Amount}",
            message.CorrelationId, message.CardId, message.Amount);

        try
        {
            if (await dbContext.CardTransactions.AnyAsync(x => x.Description == $"Saga:{message.CorrelationId}"))
            {
                logger.LogInformation("Card deduction already processed: CorrelationId={CorrelationId}", message.CorrelationId);
                await context.Publish<ICardDeductionSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    CardId = message.CardId,
                    NewBalance = 0,
                    SucceededAt = DateTime.UtcNow
                });
                return;
            }

            var card = await cardRepository.GetByIdAsync(message.CardId);
            if (card == null)
            {
                logger.LogError("Card not found: CardId={CardId}", message.CardId);
                await context.Publish<ICardDeductionFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    CardId = message.CardId,
                    Reason = "Card not found",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            var availableCredit = card.CreditLimit - card.OutstandingBalance;
            if (availableCredit < message.Amount)
            {
                logger.LogWarning("Insufficient available credit: CardId={CardId}, CreditLimit={CreditLimit}, AvailableCredit={AvailableCredit}, PaymentAmount={Amount}",
                    message.CardId, card.CreditLimit, availableCredit, message.Amount);
                await context.Publish<ICardDeductionFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    CardId = message.CardId,
                    Reason = "Insufficient credit limit to pay",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            var oldBalance = card.OutstandingBalance;
            card.OutstandingBalance = card.OutstandingBalance + message.Amount;
            card.UpdatedAtUtc = DateTime.UtcNow;

            dbContext.CardTransactions.Add(new CardTransaction
            {
                Id = Guid.NewGuid(),
                CardId = card.Id,
                UserId = card.UserId,
                Type = TransactionType.Payment,
                Amount = message.Amount,
                Description = $"Saga:{message.CorrelationId}",
                DateUtc = DateTime.UtcNow
            });

            await cardRepository.UpdateAsync(card);
            await dbContext.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("Card deduction succeeded: CorrelationId={CorrelationId}, CardId={CardId}, OldBalance={OldBalance}, NewBalance={NewBalance}",
                message.CorrelationId, message.CardId, oldBalance, card.OutstandingBalance);

            await context.Publish<ICardDeductionSucceeded>(new
            {
                CorrelationId = message.CorrelationId,
                CardId = message.CardId,
                NewBalance = card.OutstandingBalance,
                SucceededAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Card deduction failed: CorrelationId={CorrelationId}, CardId={CardId}",
                message.CorrelationId, message.CardId);

            await context.Publish<ICardDeductionFailed>(new
            {
                CorrelationId = message.CorrelationId,
                CardId = message.CardId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
