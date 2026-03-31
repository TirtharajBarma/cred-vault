using MassTransit;
using Microsoft.Extensions.Logging;
using BillingService.Application.Commands.Bills;
using MediatR;
using Shared.Contracts.Events.Payment;

namespace BillingService.API.Messaging;

public class PaymentCompletedConsumer(IMediator mediator, ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        var paymentId = context.Message.PaymentId;
        var billId = context.Message.BillId;
        var amount = context.Message.Amount;

        logger.LogInformation("PaymentCompleted received: PaymentId={PaymentId}, BillId={BillId}, Amount={Amount}",
            paymentId, billId, amount);

        try
        {
            var result = await mediator.Send(new MarkBillPaidCommand(context.Message.UserId, billId, amount));
            if (result.Success)
            {
                logger.LogInformation("Bill {BillId} marked as paid via RabbitMQ", billId);
            }
            else
            {
                logger.LogError("Failed to mark bill {BillId} as paid: {Message}", billId, result.Message);
                throw new InvalidOperationException($"Bill update failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing PaymentCompleted: PaymentId={PaymentId}", paymentId);
            throw;
        }
    }
}

public class PaymentReversedConsumer(IMediator mediator, ILogger<PaymentReversedConsumer> logger) : IConsumer<IPaymentReversed>
{
    public async Task Consume(ConsumeContext<IPaymentReversed> context)
    {
        var paymentId = context.Message.PaymentId;
        var billId = context.Message.BillId;
        var amount = context.Message.Amount;

        logger.LogInformation("PaymentReversed received: PaymentId={PaymentId}, BillId={BillId}, Amount={Amount}",
            paymentId, billId, amount);

        try
        {
            var result = await mediator.Send(new RevertBillPaidCommand(context.Message.UserId, billId, amount));
            if (result.Success)
            {
                logger.LogInformation("Bill {BillId} reverted to pending via RabbitMQ", billId);
            }
            else
            {
                logger.LogError("Failed to revert bill {BillId}: {Message}", billId, result.Message);
                throw new InvalidOperationException($"Bill revert failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing PaymentReversed: PaymentId={PaymentId}", paymentId);
            throw;
        }
    }
}
