using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Identity;
using Shared.Contracts.Events.Card;
using Shared.Contracts.Events.Billing;
using Shared.Contracts.Events.Payment;
using NotificationService.Application.Commands;

namespace NotificationService.Application.Consumers;

public class DomainEventConsumer(IMediator mediator, ILogger<DomainEventConsumer> logger) 
    : IConsumer<IUserRegistered>, IConsumer<IUserOtpGenerated>, IConsumer<ICardAdded>, IConsumer<IBillGenerated>, 
      IConsumer<IPaymentOtpGenerated>, IConsumer<IPaymentCompleted>, IConsumer<IPaymentFailed>,
      IConsumer<IUserDeleted>
{
    public async Task Consume(ConsumeContext<IUserRegistered> context)
    {
        logger.LogDebug("UserRegistered: {UserId}, {Email}", context.Message.UserId, context.Message.Email);
        await mediator.Send(new ProcessNotificationCommand("UserRegistered", context.Message.Email, context.Message.FullName,
            new { context.Message.UserId, context.Message.CreatedAtUtc }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IUserOtpGenerated> context)
    {
        logger.LogDebug("UserOtpGenerated: {Email}, Purpose={Purpose}", context.Message.Email, context.Message.Purpose);
        await mediator.Send(new ProcessNotificationCommand("UserOtpGenerated", context.Message.Email, context.Message.FullName,
            new { context.Message.OtpCode, context.Message.ExpiresAtUtc, context.Message.Purpose }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<ICardAdded> context)
    {
        logger.LogDebug("CardAdded: {CardId}, Last4={Last4}", context.Message.CardId, context.Message.CardNumberLast4);
        await mediator.Send(new ProcessNotificationCommand("CardAdded", context.Message.Email, context.Message.FullName,
            new { context.Message.CardId, context.Message.CardNumberLast4, context.Message.CardHolderName, context.Message.AddedAt }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IBillGenerated> context)
    {
        logger.LogDebug("BillGenerated: {BillId}, Amount={Amount}", context.Message.BillId, context.Message.Amount);
        await mediator.Send(new ProcessNotificationCommand("BillGenerated", context.Message.Email, context.Message.FullName,
            new { context.Message.BillId, context.Message.Amount, context.Message.DueDate, context.Message.GeneratedAt }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IPaymentOtpGenerated> context)
    {
        logger.LogDebug("PaymentOtpGenerated: {PaymentId}, Amount={Amount}", context.Message.PaymentId, context.Message.Amount);
        await mediator.Send(new ProcessNotificationCommand("PaymentOtpGenerated", context.Message.Email, context.Message.FullName,
            new { context.Message.PaymentId, context.Message.Amount, context.Message.OtpCode, context.Message.ExpiresAtUtc }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        logger.LogDebug("PaymentCompleted: {PaymentId}, Amount={Amount}", context.Message.PaymentId, context.Message.Amount);
        await mediator.Send(new ProcessNotificationCommand("PaymentCompleted", context.Message.Email, context.Message.FullName,
            new { context.Message.PaymentId, context.Message.UserId, context.Message.Amount }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IPaymentFailed> context)
    {
        logger.LogWarning("PaymentFailed: {PaymentId}, Reason={Reason}", context.Message.PaymentId, context.Message.Reason);
        await mediator.Send(new ProcessNotificationCommand("PaymentFailed", context.Message.Email, context.Message.FullName,
            new { context.Message.PaymentId, context.Message.UserId, context.Message.Amount, context.Message.Reason }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }

    public async Task Consume(ConsumeContext<IUserDeleted> context)
    {
        logger.LogInformation("UserDeleted: {UserId}", context.Message.UserId);
        await mediator.Send(new ProcessNotificationCommand("UserDeleted", "tirtharajbarma3@gmail.com", "Admin",
            new { context.Message.UserId, context.Message.DeletedAtUtc }, context.CorrelationId?.ToString(), context.MessageId?.ToString()));
    }
}
