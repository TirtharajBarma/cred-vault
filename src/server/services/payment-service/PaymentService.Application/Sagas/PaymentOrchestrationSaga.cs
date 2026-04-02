using MassTransit;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using Shared.Contracts.Events.Payment;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Sagas;

public class PaymentOrchestrationSaga : MassTransitStateMachine<PaymentOrchestrationSagaState>
{
    public new State Initial { get; private set; } = null!;
    public State AwaitingOtpVerification { get; private set; } = null!;
    public State AwaitingPaymentConfirmation { get; private set; } = null!;
    public State AwaitingBillUpdate { get; private set; } = null!;
    public State AwaitingCardDeduction { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Compensating { get; private set; } = null!;
    public State Compensated { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<IStartPaymentOrchestration> StartOrchestration { get; private set; } = null!;
    public Event<IOtpVerified> OtpVerified { get; private set; } = null!;
    public Event<IOtpFailed> OtpFailed { get; private set; } = null!;
    public Event<IPaymentProcessSucceeded> PaymentSucceeded { get; private set; } = null!;
    public Event<IPaymentProcessFailed> PaymentFailed { get; private set; } = null!;
    public Event<IBillUpdateSucceeded> BillUpdateSucceeded { get; private set; } = null!;
    public Event<IBillUpdateFailed> BillUpdateFailed { get; private set; } = null!;
    public Event<ICardDeductionSucceeded> CardDeductionSucceeded { get; private set; } = null!;
    public Event<ICardDeductionFailed> CardDeductionFailed { get; private set; } = null!;
    public Event<IRevertBillUpdateSucceeded> RevertBillSucceeded { get; private set; } = null!;
    public Event<IRevertBillUpdateFailed> RevertBillFailed { get; private set; } = null!;
    public Event<IRevertPaymentSucceeded> RevertPaymentSucceeded { get; private set; } = null!;
    public Event<IRevertPaymentFailed> RevertPaymentFailed { get; private set; } = null!;

    public PaymentOrchestrationSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => StartOrchestration, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => OtpVerified, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => OtpFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => PaymentSucceeded, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => PaymentFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => BillUpdateSucceeded, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => BillUpdateFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CardDeductionSucceeded, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => CardDeductionFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => RevertBillSucceeded, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => RevertBillFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => RevertPaymentSucceeded, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));
        Event(() => RevertPaymentFailed, x => x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Initially(
            When(StartOrchestration)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentId = ctx.Message.PaymentId;
                    ctx.Saga.UserId = ctx.Message.UserId;
                    ctx.Saga.Email = ctx.Message.Email;
                    ctx.Saga.FullName = ctx.Message.FullName;
                    ctx.Saga.CardId = ctx.Message.CardId;
                    ctx.Saga.BillId = ctx.Message.BillId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.PaymentType = ctx.Message.PaymentType;
                    ctx.Saga.OtpCode = ctx.Message.OtpCode;
                    ctx.Saga.OtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
                    ctx.Saga.CreatedAtUtc = DateTime.UtcNow;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(AwaitingOtpVerification)
        );

        During(AwaitingOtpVerification,
            When(OtpVerified)
                .Then(ctx =>
                {
                    ctx.Saga.OtpVerified = true;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(AwaitingPaymentConfirmation)
                .PublishAsync(ctx => ctx.Init<IPaymentProcessRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                })),
            When(OtpFailed)
                .Then(ctx =>
                {
                    ctx.Saga.CompensationReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
        );

        During(AwaitingPaymentConfirmation,
            When(PaymentSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentProcessed = true;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(AwaitingBillUpdate)
                .PublishAsync(ctx => ctx.Init<IBillUpdateRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.BillId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                })),
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentError = ctx.Message.Reason;
                    ctx.Saga.CompensationReason = ctx.Message.Reason;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Failed)
        );

        During(AwaitingBillUpdate,
            When(BillUpdateSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.BillUpdated = true;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(AwaitingCardDeduction)
                .PublishAsync(ctx => ctx.Init<ICardDeductionRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.CardId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                })),
            When(BillUpdateFailed)
                .Then(ctx =>
                {
                    ctx.Saga.BillUpdateError = ctx.Message.Reason;
                    ctx.Saga.CompensationReason = $"Bill update failed: {ctx.Message.Reason}";
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Compensating)
                .PublishAsync(ctx => ctx.Init<IRevertPaymentRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    PaymentId = ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.BillId,
                    ctx.Saga.CardId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                }))
        );

        During(AwaitingCardDeduction,
            When(CardDeductionSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.CardDeducted = true;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Completed)
                .PublishAsync(ctx => ctx.Init<IPaymentCompleted>(new
                {
                    PaymentId = ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    Email = ctx.Saga.Email ?? string.Empty,
                    FullName = ctx.Saga.FullName ?? "User",
                    ctx.Saga.Amount,
                    CompletedAt = DateTime.UtcNow
                })),
            When(CardDeductionFailed)
                .Then(ctx =>
                {
                    ctx.Saga.CardDeductionError = ctx.Message.Reason;
                    ctx.Saga.CompensationReason = $"Card deduction failed: {ctx.Message.Reason}";
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Compensating)
                .PublishAsync(ctx => ctx.Init<IRevertBillUpdateRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    PaymentId = ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.BillId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                }))
        );

        During(Compensating,
            When(RevertBillSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.BillUpdated = false;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .PublishAsync(ctx => ctx.Init<IRevertPaymentRequested>(new
                {
                    CorrelationId = ctx.Saga.CorrelationId,
                    PaymentId = ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    ctx.Saga.BillId,
                    ctx.Saga.CardId,
                    ctx.Saga.Amount,
                    RequestedAt = DateTime.UtcNow
                })),
            When(RevertBillFailed)
                .Then(ctx =>
                {
                    ctx.Saga.CompensationAttempts++;
                    ctx.Saga.CompensationReason += $" | Bill revert failed: {ctx.Message.Reason}";
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .If(ctx => ctx.Saga.CompensationAttempts >= 3,
                    x => x.TransitionTo(Failed))
                .If(ctx => ctx.Saga.CompensationAttempts < 3,
                    x => x.TransitionTo(Compensating)
                        .PublishAsync(ctx => ctx.Init<IRevertBillUpdateRequested>(new
                        {
                            CorrelationId = ctx.Saga.CorrelationId,
                            PaymentId = ctx.Saga.PaymentId,
                            ctx.Saga.UserId,
                            ctx.Saga.BillId,
                            ctx.Saga.Amount,
                            RequestedAt = DateTime.UtcNow
                        }))
                )
        );

        During(Compensating,
            When(RevertPaymentSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.PaymentProcessed = false;
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Compensated)
                .PublishAsync(ctx => ctx.Init<IPaymentFailed>(new
                {
                    PaymentId = ctx.Saga.PaymentId,
                    ctx.Saga.UserId,
                    Email = ctx.Saga.Email ?? string.Empty,
                    FullName = ctx.Saga.FullName ?? "User",
                    ctx.Saga.Amount,
                    Reason = ctx.Saga.CompensationReason ?? "Payment failed and compensated",
                    FailedAt = DateTime.UtcNow
                })),
            When(RevertPaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.CompensationAttempts++;
                    ctx.Saga.CompensationReason += $" | Payment revert failed: {ctx.Message.Reason}";
                    ctx.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .If(ctx => ctx.Saga.CompensationAttempts >= 5,
                    x => x.TransitionTo(Failed))
                .If(ctx => ctx.Saga.CompensationAttempts < 5,
                    x => x.TransitionTo(Compensating)
                        .PublishAsync(ctx => ctx.Init<IRevertPaymentRequested>(new
                        {
                            CorrelationId = ctx.Saga.CorrelationId,
                            PaymentId = ctx.Saga.PaymentId,
                            ctx.Saga.UserId,
                            ctx.Saga.BillId,
                            ctx.Saga.CardId,
                            ctx.Saga.Amount,
                            RequestedAt = DateTime.UtcNow
                        }))
                )
        );

        During(Completed,
            Ignore(PaymentSucceeded),
            Ignore(BillUpdateSucceeded),
            Ignore(CardDeductionSucceeded)
        );

        During(Compensated,
            Ignore(RevertBillSucceeded),
            Ignore(RevertPaymentSucceeded)
        );

        During(Failed,
            Ignore(OtpFailed),
            Ignore(PaymentFailed),
            Ignore(BillUpdateFailed),
            Ignore(CardDeductionFailed),
            Ignore(RevertBillFailed),
            Ignore(RevertPaymentFailed)
        );

        SetCompletedWhenFinalized();
    }
}
