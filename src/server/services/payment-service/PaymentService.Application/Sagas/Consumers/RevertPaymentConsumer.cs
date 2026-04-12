using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Sagas;

public class RevertPaymentConsumer(
    IPaymentRepository paymentRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ILogger<RevertPaymentConsumer> logger
) : IConsumer<IRevertPaymentRequested>
{
    public async Task Consume(ConsumeContext<IRevertPaymentRequested> context)
    {
        var message = context.Message;

        logger.LogInformation("Reverting payment: PaymentId={PaymentId}, Amount={Amount}",
            message.PaymentId, message.Amount);

        try
        {
            var payment = await paymentRepository.GetByIdAsync(message.PaymentId);

            if (payment == null)
            {
                logger.LogWarning("Payment not found for revert: PaymentId={PaymentId}", message.PaymentId);
                await context.Publish<IRevertPaymentFailed>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    Reason = "Payment not found",
                    FailedAt = DateTime.UtcNow
                });
                return;
            }

            if (payment.Status == PaymentStatus.Reversed)
            {
                logger.LogInformation("Payment already reversed: PaymentId={PaymentId}", message.PaymentId);
                await context.Publish<IRevertPaymentSucceeded>(new
                {
                    CorrelationId = message.CorrelationId,
                    PaymentId = message.PaymentId,
                    SucceededAt = DateTime.UtcNow
                });
                return;
            }

            payment.Status = PaymentStatus.Reversed;
            payment.UpdatedAtUtc = DateTime.UtcNow;
            await paymentRepository.UpdateAsync(payment);

            var reversalTransaction = new Domain.Entities.Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Type = PaymentTransactionType.Reversal,
                Description = $"Saga compensation: {message.CorrelationId}",
                CreatedAtUtc = DateTime.UtcNow
            };
            await transactionRepository.AddAsync(reversalTransaction);

            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            await context.Publish<IPaymentReversed>(new
            {
                PaymentId = payment.Id,
                UserId = payment.UserId,
                BillId = payment.BillId,
                CardId = payment.CardId,
                Amount = payment.Amount,
                PointsDeducted = 0,
                ReversedAt = DateTime.UtcNow
            });

            logger.LogInformation("Payment reverted successfully: PaymentId={PaymentId}", message.PaymentId);

            await context.Publish<IRevertPaymentSucceeded>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                SucceededAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to revert payment: PaymentId={PaymentId}", message.PaymentId);

            await context.Publish<IRevertPaymentFailed>(new
            {
                CorrelationId = message.CorrelationId,
                PaymentId = message.PaymentId,
                Reason = ex.Message,
                FailedAt = DateTime.UtcNow
            });
        }
    }
}
