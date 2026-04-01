using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Infrastructure.Messaging.Consumers;

public class PaymentCompletedConsumer(IPaymentRepository payments, ITransactionRepository transactions, IUnitOfWork uow, ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        var paymentId = context.Message.PaymentId;
        logger.LogInformation("IPaymentCompleted received: PaymentId={PaymentId}", paymentId);

        try
        {
            var payment = await payments.GetByIdAsync(paymentId);
            if (payment == null)
            {
                logger.LogError("Payment {PaymentId} not found", paymentId);
                throw new InvalidOperationException($"Payment {paymentId} not found");
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                logger.LogInformation("Payment {PaymentId} already completed (idempotency)", paymentId);
                return;
            }

            payment.Status = PaymentStatus.Completed;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await payments.UpdateAsync(payment);

            await transactions.AddAsync(new Transaction
            {
                Id = Guid.NewGuid(), PaymentId = payment.Id, UserId = payment.UserId,
                Amount = payment.Amount, Type = TransactionType.Payment,
                Description = "Payment completed", CreatedAtUtc = DateTime.UtcNow
            });

            await uow.SaveChangesAsync();
            logger.LogInformation("Payment {PaymentId} marked as completed", payment.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IPaymentCompleted: PaymentId={PaymentId}", paymentId);
            throw;
        }
    }
}

public class PaymentFailedConsumer(IPaymentRepository payments, IUnitOfWork uow, ILogger<PaymentFailedConsumer> logger) : IConsumer<IPaymentFailed>
{
    public async Task Consume(ConsumeContext<IPaymentFailed> context)
    {
        var paymentId = context.Message.PaymentId;
        var reason = context.Message.Reason;
        logger.LogWarning("IPaymentFailed received: PaymentId={PaymentId}, Reason={Reason}", paymentId, reason);

        try
        {
            var payment = await payments.GetByIdAsync(paymentId);
            if (payment == null)
            {
                logger.LogError("Payment {PaymentId} not found for failure event", paymentId);
                throw new InvalidOperationException($"Payment {paymentId} not found");
            }

            if (payment.Status is PaymentStatus.Failed or PaymentStatus.Reversed)
            {
                logger.LogInformation("Payment {PaymentId} already in terminal state {Status}", paymentId, payment.Status);
                return;
            }

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = reason;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await payments.UpdateAsync(payment);
            await uow.SaveChangesAsync();
            logger.LogWarning("Payment {PaymentId} marked as failed: {Reason}", paymentId, reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process IPaymentFailed: PaymentId={PaymentId}", paymentId);
            throw;
        }
    }
}
