using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Services;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Sagas;

public class PaymentProcessConsumer(
    IPaymentRepository paymentRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    IWalletService walletService,
    ILogger<PaymentProcessConsumer> logger
) : IConsumer<IPaymentProcessRequested>                 // saga
{
    public async Task Consume(ConsumeContext<IPaymentProcessRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("Processing payment: PaymentId={PaymentId}, Amount={Amount}",
            message.PaymentId, message.Amount);

        try
        {
            var payment = await paymentRepository.GetByIdAsync(message.PaymentId);

            if (payment == null)
            {
                logger.LogError("Payment not found: PaymentId={PaymentId}", message.PaymentId);
                await context.Publish<IPaymentProcessFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    Reason = "Payment not found",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            if (payment.Status == PaymentStatus.Completed)
            {
                logger.LogInformation("Payment already completed: PaymentId={PaymentId}", message.PaymentId);
                await context.Publish<IPaymentProcessSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    SucceededAt = DateTime.UtcNow
                });
                return;
            }


            // wallet deduction happens
            var walletDeducted = await walletService.DeductAsync(
                message.UserId,
                message.Amount,
                message.PaymentId,
                $"Bill payment for Bill {message.PaymentId}"
            );

            if (!walletDeducted)
            {
                logger.LogWarning("Wallet deduction failed for PaymentId={PaymentId}, UserId={UserId}, Amount={Amount}",
                    message.PaymentId, message.UserId, message.Amount);
                
                await context.Publish<IPaymentProcessFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    Reason = "Insufficient wallet balance",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            payment.Status = PaymentStatus.Processing;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await paymentRepository.UpdateAsync(payment);
            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("Payment processed successfully: PaymentId={PaymentId}, Wallet deducted: {Amount}",
                message.PaymentId, message.Amount);

            await context.Publish<IPaymentProcessSucceeded>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                SucceededAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process payment: PaymentId={PaymentId}", message.PaymentId);

            await context.Publish<IPaymentProcessFailed>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
