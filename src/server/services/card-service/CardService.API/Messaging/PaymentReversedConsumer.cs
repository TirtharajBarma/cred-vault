using MassTransit;
using Microsoft.Extensions.Logging;
using MediatR;
using Shared.Contracts.Events.Payment;
using CardService.Application.Commands.Cards;

namespace CardService.API.Messaging;

public class PaymentReversedConsumer(IMediator mediator, ILogger<PaymentReversedConsumer> logger) : IConsumer<IPaymentReversed>
{
    public async Task Consume(ConsumeContext<IPaymentReversed> context)
    {
        var message = context.Message;

        logger.LogInformation("PaymentReversed received: PaymentId={PaymentId}, CardId={CardId}, Amount={Amount}",
            message.PaymentId, message.CardId, message.Amount);

        try
        {
            var result = await mediator.Send(new RefundCardBalanceCommand(
                message.CardId,
                message.UserId,
                message.PaymentId,
                message.Amount
            ), context.CancellationToken);

            if (result.Success)
            {
                logger.LogInformation("Card refund successful: PaymentId={PaymentId}, NewBalance={NewBalance}",
                    message.PaymentId, result.NewBalance);
            }
            else
            {
                logger.LogError("Card refund failed: PaymentId={PaymentId}, Error={Error}",
                    message.PaymentId, result.Message);
                throw new InvalidOperationException($"Card refund failed: {result.Message}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Error processing PaymentReversed: PaymentId={PaymentId}", message.PaymentId);
            throw;
        }
    }
}
