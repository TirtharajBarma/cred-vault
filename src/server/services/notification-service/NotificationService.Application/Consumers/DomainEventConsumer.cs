using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.Identity;
using Shared.Contracts.Events.Billing;
using Shared.Contracts.Events.Payment;
using Shared.Contracts.Events.Saga;
using Shared.Contracts.Events.Card;
using NotificationService.Application.Commands;

namespace NotificationService.Application.Consumers;

public class DomainEventConsumer(IMediator mediator, ILogger<DomainEventConsumer> logger) 
    : IConsumer<IUserRegistered>, IConsumer<IUserOtpGenerated>, IConsumer<IBillGenerated>, 
      IConsumer<IPaymentOtpGenerated>, IConsumer<IPaymentCompleted>, IConsumer<IPaymentFailed>,
      IConsumer<IOtpFailed>, IConsumer<ICardAdded>
{
    public async Task Consume(ConsumeContext<IUserRegistered> context)
    {
        logger.LogDebug("UserRegistered: {UserId}, {Email}", context.Message.UserId, context.Message.Email);
        
        var payload = new
        {
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.CreatedAtUtc
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "UserRegistered",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IUserOtpGenerated> context)
    {
        logger.LogDebug("UserOtpGenerated: {Email}, Purpose={Purpose}", context.Message.Email, context.Message.Purpose);
        
        var payload = new
        {
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.OtpCode,
            context.Message.Purpose,
            context.Message.ExpiresAtUtc
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "UserOtpGenerated",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IBillGenerated> context)
    {
        logger.LogDebug("BillGenerated: {BillId}, Amount={Amount}", context.Message.BillId, context.Message.Amount);
        
        var payload = new
        {
            context.Message.BillId,
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.Amount,
            context.Message.CardId,
            context.Message.DueDate,
            context.Message.GeneratedAt
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "BillGenerated",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IPaymentOtpGenerated> context)
    {
        logger.LogDebug("PaymentOtpGenerated: {PaymentId}, Amount={Amount}", context.Message.PaymentId, context.Message.Amount);
        
        var payload = new
        {
            context.Message.PaymentId,
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.Amount,
            context.Message.OtpCode,
            context.Message.ExpiresAtUtc
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentOtpGenerated",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IPaymentCompleted> context)
    {
        logger.LogDebug("PaymentCompleted: {PaymentId}, Amount={Amount}", context.Message.PaymentId, context.Message.Amount);
        
        var payload = new
        {
            context.Message.PaymentId,
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.Amount,
            context.Message.AmountPaid,
            context.Message.RewardsRedeemed,
            context.Message.CardId,
            context.Message.BillId,
            context.Message.CompletedAt
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentCompleted",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IPaymentFailed> context)
    {
        logger.LogWarning("PaymentFailed: {PaymentId}, Reason={Reason}", context.Message.PaymentId, context.Message.Reason);
        
        var payload = new
        {
            context.Message.PaymentId,
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.Amount,
            context.Message.Reason,
            context.Message.FailedAt
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "PaymentFailed",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<IOtpFailed> context)
    {
        logger.LogWarning("OtpFailed: CorrelationId={CorrelationId}, PaymentId={PaymentId}, Reason={Reason}", 
            context.Message.CorrelationId, context.Message.PaymentId, context.Message.Reason);
        
        var payload = new
        {
            context.Message.CorrelationId,
            context.Message.PaymentId,
            context.Message.Reason,
            context.Message.FailedAt
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "OtpFailed",
            null,
            "User",
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }

    public async Task Consume(ConsumeContext<ICardAdded> context)
    {
        logger.LogInformation("CardAdded: CardId={CardId}, UserId={UserId}, Email={Email}", 
            context.Message.CardId, context.Message.UserId, context.Message.Email);
        
        var payload = new
        {
            context.Message.CardId,
            context.Message.UserId,
            context.Message.Email,
            context.Message.FullName,
            context.Message.CardNumberLast4,
            context.Message.CardHolderName,
            context.Message.AddedAt
        };
        
        await mediator.Send(new ProcessNotificationCommand(
            "CardAdded",
            context.Message.Email,
            context.Message.FullName,
            payload,
            context.CorrelationId?.ToString(),
            context.MessageId?.ToString()
        ));
    }
}
