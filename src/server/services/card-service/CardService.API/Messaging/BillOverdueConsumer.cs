using MassTransit;
using Microsoft.Extensions.Logging;
using MediatR;
using CardService.Application.Commands.Cards;
using Shared.Contracts.Events.Saga;

namespace CardService.API.Messaging;

public class BillOverdueConsumer(
    IMediator mediator,
    ILogger<BillOverdueConsumer> logger
) : IConsumer<IBillOverdueDetected>
{
    public async Task Consume(ConsumeContext<IBillOverdueDetected> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "BillOverdueConsumer: BillId={BillId}, CardId={CardId}, DaysOverdue={DaysOverdue}",
            message.BillId, message.CardId, message.DaysOverdue);

        try
        {
            var result = await mediator.Send(
                new ApplyStrikeCommand(message.CardId, message.BillId, $"Bill overdue by {message.DaysOverdue} days"),
                context.CancellationToken);

            if (result.Success && result.Data != null)
            {
                logger.LogInformation(
                    "Strike applied: CardId={CardId}, Strikes={Strikes}, Blocked={Blocked}",
                    message.CardId, result.Data.NewStrikeCount, result.Data.IsCardBlocked);

                if (result.Data.IsCardBlocked)
                {
                    await context.Publish<ICardBlocked>(new
                    {
                        CardId = message.CardId,
                        UserId = message.UserId,
                        StrikeCount = result.Data.NewStrikeCount,
                        Reason = $"Card blocked after {result.Data.NewStrikeCount} strikes due to overdue bills",
                        BlockedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                logger.LogWarning(
                    "Failed to apply strike for CardId={CardId}: {Reason}",
                    message.CardId, result.Message ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error applying strike for BillId={BillId}", message.BillId);
        }
    }
}
