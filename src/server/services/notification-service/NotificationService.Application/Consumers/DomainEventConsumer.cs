using MassTransit;
using MediatR;
using Shared.Contracts.Events.Identity;
using Shared.Contracts.Events.Card;
using Shared.Contracts.Events.Billing;
using Shared.Contracts.Events.Payment;

namespace NotificationService.Application.Consumers;

public class DomainEventConsumer(IMediator mediator) :
    IConsumer<IUserRegistered>,
    IConsumer<IUserOtpGenerated>,
    IConsumer<ICardAdded>,
    IConsumer<IBillGenerated>,
    IConsumer<IPaymentOtpGenerated>,
    IConsumer<IPaymentCompleted>,
    IConsumer<IPaymentFailed>,
    IConsumer<IFraudDetected>
{
    public async Task Consume(ConsumeContext<IUserRegistered> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "UserRegistered",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.UserId, context.Message.CreatedAtUtc },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IUserOtpGenerated> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "UserOtpGenerated",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.OtpCode, context.Message.ExpiresAtUtc, context.Message.Purpose },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<ICardAdded> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "CardAdded",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.CardId, context.Message.CardNumberLast4, context.Message.CardHolderName, context.Message.AddedAt },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IBillGenerated> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "BillGenerated",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.BillId, context.Message.Amount, context.Message.DueDate, context.Message.GeneratedAt },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IPaymentOtpGenerated> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentOtpGenerated",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.PaymentId, context.Message.Amount, context.Message.OtpCode, context.Message.ExpiresAtUtc },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IPaymentCompleted> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentCompleted",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.PaymentId, context.Message.UserId, context.Message.Amount, context.Message.RiskScore, context.Message.RiskDecision },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IPaymentFailed> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentFailed",
            context.Message.Email,
            context.Message.FullName,
            new { context.Message.PaymentId, context.Message.UserId, context.Message.Amount, context.Message.Reason },
            context.CorrelationId?.ToString()
        ));

    public async Task Consume(ConsumeContext<IFraudDetected> context) =>
        await mediator.Send(new ProcessNotificationCommand(
            "FraudDetected",
            "security@credvault.com",
            "Security Team",
            new { context.Message.PaymentId, context.Message.UserId, context.Message.RiskScore, context.Message.AlertType },
            context.CorrelationId?.ToString()
        ));
}

public record ProcessNotificationCommand(
    string EventType,
    string Email,
    string FullName,
    object Payload,
    string? TraceId) : IRequest;
