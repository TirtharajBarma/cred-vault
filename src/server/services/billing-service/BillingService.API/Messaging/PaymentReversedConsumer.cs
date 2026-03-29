using BillingService.Application.Commands.Bills;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Payment;

namespace BillingService.API.Messaging;

public class PaymentReversedConsumer(
    IMediator mediator,
    ILogger<PaymentReversedConsumer> logger) : IConsumer<IPaymentReversed>
{
    public async Task Consume(ConsumeContext<IPaymentReversed> context)
    {
        try
        {
            var msg = context.Message;
            logger.LogInformation("PaymentReversed received: PaymentId={PaymentId} BillId={BillId} UserId={UserId} Amount={Amount}", 
                msg.PaymentId, msg.BillId, msg.UserId, msg.Amount);

            var command = new RevertBillPaidCommand(msg.UserId, msg.BillId, msg.Amount);
            var result = await mediator.Send(command);

            if (result.Success)
            {
                logger.LogInformation("Bill {BillId} reverted to Pending via RabbitMQ. Rewards deducted.", msg.BillId);
            }
            else
            {
                logger.LogWarning("Failed to revert Bill {BillId}: {Message}", msg.BillId, result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing PaymentReversed: {Message}", ex.Message);
            throw;
        }
    }
}
