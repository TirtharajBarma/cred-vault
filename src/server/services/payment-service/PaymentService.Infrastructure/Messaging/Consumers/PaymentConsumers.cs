using MassTransit;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Infrastructure.Messaging.Consumers;

// Handles IPaymentCompleted — marks payment Completed + inserts Transaction + persists RiskScore
public class PaymentCompletedConsumer(
    IPaymentRepository paymentRepository,
    ITransactionRepository transactionRepository,
    IRiskRepository riskRepository,
    IUnitOfWork unitOfWork) : IConsumer<IPaymentCompleted>
{
    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        var payment = await paymentRepository.GetByIdAsync(context.Message.PaymentId);
        if (payment is null) return;

        // Idempotency guard
        if (payment.Status == PaymentStatus.Completed) return;

        payment.Status = PaymentStatus.Completed;
        payment.UpdatedAtUtc = DateTime.UtcNow;
        await paymentRepository.UpdateAsync(payment);

        await transactionRepository.AddAsync(new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Amount = payment.Amount,
            Type = TransactionType.Payment,
            Description = "Payment completed successfully",
            CreatedAtUtc = DateTime.UtcNow
        });

        // Persist RiskScore for approved payments (auto or OTP)
        var existing = await riskRepository.GetByPaymentIdAsync(payment.Id);
        if (existing is null)
        {
            var decision = Enum.TryParse<RiskDecision>(context.Message.RiskDecision, out var d)
                ? d
                : RiskDecision.AutoApproved;

            await riskRepository.AddAsync(new RiskScore
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                UserId = payment.UserId,
                Score = context.Message.RiskScore,
                Decision = decision,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await unitOfWork.SaveChangesAsync();
    }
}

// Handles IPaymentFailed — marks payment Failed
public class PaymentFailedConsumer(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork) : IConsumer<IPaymentFailed>
{
    public async Task Consume(ConsumeContext<IPaymentFailed> context)
    {
        var payment = await paymentRepository.GetByIdAsync(context.Message.PaymentId);
        if (payment is null) return;

        if (payment.Status is PaymentStatus.Failed or PaymentStatus.Reversed) return;

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = context.Message.Reason;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await paymentRepository.UpdateAsync(payment);
        await unitOfWork.SaveChangesAsync();
    }
}

// Handles IFraudDetected — inserts FraudAlert + RiskScore for blocked payments
public class FraudDetectedConsumer(
    IFraudRepository fraudRepository,
    IRiskRepository riskRepository,
    IUnitOfWork unitOfWork) : IConsumer<IFraudDetected>
{
    public async Task Consume(ConsumeContext<IFraudDetected> context)
    {
        // Idempotency — don't insert duplicates
        var existingRisk = await riskRepository.GetByPaymentIdAsync(context.Message.PaymentId);
        if (existingRisk is null)
        {
            await riskRepository.AddAsync(new RiskScore
            {
                Id = Guid.NewGuid(),
                PaymentId = context.Message.PaymentId,
                UserId = context.Message.UserId,
                Score = context.Message.RiskScore,
                Decision = RiskDecision.Blocked,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var existingAlert = await fraudRepository.GetByPaymentIdAsync(context.Message.PaymentId);
        if (existingAlert is null)
        {
            await fraudRepository.AddAsync(new FraudAlert
            {
                Id = Guid.NewGuid(),
                PaymentId = context.Message.PaymentId,
                UserId = context.Message.UserId,
                RiskScore = context.Message.RiskScore,
                AlertType = context.Message.AlertType,
                Status = FraudAlertStatus.Open,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await unitOfWork.SaveChangesAsync();
    }
}
