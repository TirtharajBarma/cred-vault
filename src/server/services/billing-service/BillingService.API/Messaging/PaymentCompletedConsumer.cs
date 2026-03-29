using BillingService.Application.Commands.Bills;
using MassTransit;
using MediatR;
using Shared.Contracts.Events.Payment;

namespace BillingService.API.Messaging;

/// <summary>
/// Consumes PaymentCompleted events from PaymentService via RabbitMQ.
/// Marks the bill as Paid and calculates reward points.
/// This replaces the user-facing mark-paid HTTP call for event-driven payments.
/// </summary>
public class PaymentCompletedConsumer(
    IMediator mediator,
    ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        try
        {
            var msg = context.Message;
            logger.LogInformation("PaymentCompleted received: PaymentId={PaymentId} BillId={BillId} UserId={UserId} Amount={Amount}", 
                msg.PaymentId, msg.BillId, msg.UserId, msg.Amount);

            var command = new MarkBillPaidCommand(msg.UserId, msg.BillId, msg.Amount);
            var result = await mediator.Send(command);

            if (result.Success)
            {
                logger.LogInformation("Bill {BillId} marked Paid via RabbitMQ. Rewards processed.", msg.BillId);
            }
            else
            {
                logger.LogWarning("Failed to mark Bill {BillId} as paid via RabbitMQ: {Message}", msg.BillId, result.Message);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing PaymentCompleted: {Message}", ex.Message);
            throw;
        }
    }
}
