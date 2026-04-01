using MassTransit;
using Microsoft.Extensions.Logging;
using MediatR;
using CardService.Application.Commands.Cards;
using Shared.Contracts.Events.Saga;

namespace CardService.API.Messaging;

public class BillPaidConsumer(
    IMediator mediator,
    ILogger<BillPaidConsumer> logger
) : IConsumer<IBillUpdateSucceeded>
{
    public async Task Consume(ConsumeContext<IBillUpdateSucceeded> context)
    {
        var message = context.Message;

        logger.LogInformation("BillPaidConsumer: BillId={BillId}, CardId={CardId}", message.BillId, message.CardId);

        if (message.CardId == Guid.Empty)
        {
            logger.LogWarning("BillPaidConsumer received empty CardId, skipping");
            return;
        }

        try
        {
            var result = await mediator.Send(
                new ClearStrikesCommand(message.CardId),
                context.CancellationToken);

            if (result.Success)
            {
                logger.LogInformation("Strikes cleared for CardId={CardId}, BillId={BillId}", message.CardId, message.BillId);
            }
            else
            {
                logger.LogWarning("Failed to clear strikes for CardId={CardId}: {Reason}", 
                    message.CardId, result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing strikes for CardId={CardId}, BillId={BillId}", 
                message.CardId, message.BillId);
        }
    }
}
