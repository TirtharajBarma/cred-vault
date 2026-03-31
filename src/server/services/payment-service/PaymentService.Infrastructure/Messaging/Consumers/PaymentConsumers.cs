using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Infrastructure.Messaging.Consumers;

public class PaymentCompletedConsumer(IPaymentRepository payments, ITransactionRepository transactions, IRiskRepository risk, IUnitOfWork uow, ILogger<PaymentCompletedConsumer> logger) : IConsumer<IPaymentCompleted>
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

            if (await risk.GetByPaymentIdAsync(payment.Id) == null)
            {
                var decision = Enum.TryParse<RiskDecision>(context.Message.RiskDecision, out var d) ? d : RiskDecision.AutoApproved;
                await risk.AddAsync(new RiskScore
                {
                    Id = Guid.NewGuid(), PaymentId = payment.Id, UserId = payment.UserId,
                    Score = context.Message.RiskScore, Decision = decision, CreatedAtUtc = DateTime.UtcNow
                });
                logger.LogInformation("RiskScore created for {PaymentId}: Decision={Decision}", payment.Id, decision);
            }

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

public class FraudDetectedConsumer(IFraudRepository fraud, IRiskRepository risk, IUnitOfWork uow, ILogger<FraudDetectedConsumer> logger) : IConsumer<IFraudDetected>
{
    public async Task Consume(ConsumeContext<IFraudDetected> context)
    {
        var paymentId = context.Message.PaymentId;
        var userId = context.Message.UserId;
        var riskScore = context.Message.RiskScore;
        var alertType = context.Message.AlertType;

        logger.LogError("FRAUD DETECTED: PaymentId={PaymentId}, UserId={UserId}, Score={Score}, Type={Type}",
            paymentId, userId, riskScore, alertType);

        try
        {
            if (await risk.GetByPaymentIdAsync(paymentId) == null)
            {
                await risk.AddAsync(new RiskScore
                {
                    Id = Guid.NewGuid(), PaymentId = paymentId, UserId = userId,
                    Score = riskScore, Decision = RiskDecision.Blocked, CreatedAtUtc = DateTime.UtcNow
                });
                logger.LogWarning("Fraud risk score recorded: PaymentId={PaymentId}, Decision=Blocked", paymentId);
            }

            if (await fraud.GetByPaymentIdAsync(paymentId) == null)
            {
                await fraud.AddAsync(new FraudAlert
                {
                    Id = Guid.NewGuid(), PaymentId = paymentId, UserId = userId,
                    RiskScore = riskScore, AlertType = alertType,
                    Status = FraudAlertStatus.Open, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
                });
                logger.LogWarning("Fraud alert created: PaymentId={PaymentId}, Type={Type}", paymentId, alertType);
            }

            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process fraud alert: PaymentId={PaymentId}", paymentId);
            throw;
        }
    }
}
