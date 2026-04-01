using MassTransit;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Sagas;

public class PaymentSaga : MassTransitStateMachine<PaymentSagaState>
{
    public State Initiated { get; private set; } = null!;
    public State RiskCheckPassed { get; private set; } = null!;
    public State Processing { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<IPaymentInitiated> PaymentInitiated { get; private set; } = null!;
    public Event<IOTPVerified> OTPVerified { get; private set; } = null!;
    public Event<IPaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<IPaymentFailed> PaymentFailed { get; private set; } = null!;

    public PaymentSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => PaymentInitiated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
        Event(() => OTPVerified,      x => x.CorrelateById(ctx => ctx.Message.PaymentId));
        Event(() => PaymentCompleted, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
        Event(() => PaymentFailed,    x => x.CorrelateById(ctx => ctx.Message.PaymentId));

        Initially(
            When(PaymentInitiated)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId   = ctx.Message.PaymentId;
                    ctx.Saga.UserId      = ctx.Message.UserId;
                    ctx.Saga.Email       = ctx.Message.Email;
                    ctx.Saga.FullName    = ctx.Message.FullName;
                    ctx.Saga.CardId      = ctx.Message.CardId;
                    ctx.Saga.BillId      = ctx.Message.BillId;
                    ctx.Saga.Amount      = ctx.Message.Amount;
                    ctx.Saga.PaymentType = ctx.Message.PaymentType;
                    ctx.Saga.CreatedAtUtc  = DateTime.UtcNow;
                    ctx.Saga.UpdatedAtUtc  = DateTime.UtcNow;
                    ctx.Saga.RiskScore   = ctx.Message.RiskScore;
                })
                .TransitionTo(Initiated)
        );

        During(Initiated,
            When(PaymentInitiated)
                .Then(ctx =>
                {
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                // 1. BLOCKED — high risk
                .If(ctx => ctx.Saga.RiskScore >= 75,
                    blocked => blocked
                        .Then(ctx =>
                        {
                            ctx.Saga.RiskDecision = RiskDecision.Blocked.ToString();
                            ctx.Saga.CompensationReason = "Blocked due to high risk score";
                        })
                        .TransitionTo(Failed)
                        .PublishAsync(ctx => ctx.Init<IFraudDetected>(new
                        {
                            PaymentId = ctx.Saga.PaymentId,
                            UserId = ctx.Saga.UserId,
                            RiskScore = ctx.Saga.RiskScore,
                            AlertType = "HighAmount",
                            DetectedAt = DateTime.UtcNow
                        }))
                        .PublishAsync(ctx => ctx.Init<IPaymentFailed>(new
                        {
                            PaymentId = ctx.Saga.PaymentId,
                            UserId = ctx.Saga.UserId,
                            Email = ctx.Saga.Email ?? string.Empty,
                            FullName = ctx.Saga.FullName ?? "User",
                            Amount = ctx.Saga.Amount,
                            Reason = "Payment blocked by risk management."
                        })))
                // 2. OTP REQUIRED — medium risk
                .If(ctx => ctx.Saga.RiskScore >= 50 && ctx.Saga.RiskScore < 75,
                    otp => otp
                        .Then(ctx =>
                        {
                            ctx.Saga.RiskDecision    = RiskDecision.OTPRequired.ToString();
                            ctx.Saga.OtpCode         = GenerateOtp();
                            ctx.Saga.OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
                        })
                        .TransitionTo(RiskCheckPassed)
                        .PublishAsync(ctx => ctx.Init<IPaymentOtpGenerated>(new
                        {
                            PaymentId = ctx.Saga.PaymentId,
                            UserId = ctx.Saga.UserId,
                            Email = ctx.Saga.Email ?? string.Empty,
                            FullName = ctx.Saga.FullName ?? "User",
                            Amount = ctx.Saga.Amount,
                            OtpCode = ctx.Saga.OtpCode,
                            ExpiresAtUtc = ctx.Saga.OtpExpiresAtUtc
                        })))
                // 3. AUTO APPROVED — low risk
                .If(ctx => ctx.Saga.RiskScore < 50,
                    approved => approved
                        .Then(ctx =>
                        {
                            ctx.Saga.RiskDecision = RiskDecision.AutoApproved.ToString();
                        })
                        .TransitionTo(Processing)
                        .PublishAsync(ctx => ctx.Init<IPaymentCompleted>(new
                        {
                            PaymentId    = ctx.Saga.PaymentId,
                            UserId       = ctx.Saga.UserId,
                            Email        = ctx.Saga.Email ?? string.Empty,
                            FullName     = ctx.Saga.FullName ?? "User",
                            CardId       = ctx.Saga.CardId,
                            BillId       = ctx.Saga.BillId,
                            Amount       = ctx.Saga.Amount,
                            RiskScore    = ctx.Saga.RiskScore,
                            RiskDecision = RiskDecision.AutoApproved.ToString(),
                            CompletedAt  = DateTime.UtcNow
                        })))
        );

        // Waiting for OTP — score was 50-74
        During(RiskCheckPassed,
            When(OTPVerified)
                .Then(ctx => ctx.Saga.UpdatedAtUtc = DateTime.UtcNow)
                .TransitionTo(Processing)
                .PublishAsync(ctx => ctx.Init<IPaymentCompleted>(new
                {
                    PaymentId    = ctx.Saga.PaymentId,
                    UserId       = ctx.Saga.UserId,
                    Email        = ctx.Saga.Email ?? string.Empty,
                    FullName     = ctx.Saga.FullName ?? "User",
                    CardId       = ctx.Saga.CardId,
                    BillId       = ctx.Saga.BillId,
                    Amount       = ctx.Saga.Amount,
                    RiskScore    = ctx.Saga.RiskScore,
                    RiskDecision = ctx.Saga.RiskDecision ?? RiskDecision.OTPRequired.ToString(),
                    CompletedAt  = DateTime.UtcNow
                }))
        );

        // Processing — waiting for consumer to confirm completion
        During(Processing,
            When(PaymentCompleted)
                .Then(ctx => ctx.Saga.UpdatedAtUtc = DateTime.UtcNow)
                .TransitionTo(Completed),
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.CompensationReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAtUtc       = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
        );

        // Terminal states — ignore duplicate events
        DuringAny(
            When(PaymentCompleted)
                .If(ctx => ctx.Saga.CurrentState == nameof(Completed) || ctx.Saga.CurrentState == nameof(Failed),
                    x => x.Then(_ => { })),
            When(PaymentFailed)
                .If(ctx => ctx.Saga.CurrentState == nameof(Failed),
                    x => x.Then(_ => { }))
        );

        SetCompletedWhenFinalized();
    }

    // Simple rule-based risk scoring

    private static string GenerateOtp() =>
        Random.Shared.Next(100000, 999999).ToString();
}
