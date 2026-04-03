using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Payment;

namespace CardService.API.Messaging;

public class PaymentReversedConsumer(
    ILogger<PaymentReversedConsumer> logger
) : IConsumer<IPaymentReversed>
{
    public async Task Consume(ConsumeContext<IPaymentReversed> context)
    {
        var message = context.Message;

        logger.LogInformation("PaymentReversed: PaymentId={PaymentId}, CardId={CardId}, Amount={Amount} - Payment reverted", 
            message.PaymentId, message.CardId, message.Amount);
    }
}
