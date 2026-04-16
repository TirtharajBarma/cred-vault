using MediatR;

// it is like a parcel -> hold data about notification events
namespace NotificationService.Application.Commands;

public record ProcessNotificationCommand(
    string EventType,
    string? Email,
    string? FullName,
    object Payload,
    string? CorrelationId = null,
    string? MessageId = null
) : IRequest;
