using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Saga;

namespace CardService.API.Messaging;

public class BillPaidConsumer(
    ILogger<BillPaidConsumer> logger
) : IConsumer<IBillUpdateSucceeded>
{
    public async Task Consume(ConsumeContext<IBillUpdateSucceeded> context)
    {
        var message = context.Message;

        logger.LogInformation("BillPaidConsumer: BillId={BillId}, CardId={CardId} - Bill marked as paid", 
            message.BillId, message.CardId);
    }
}
