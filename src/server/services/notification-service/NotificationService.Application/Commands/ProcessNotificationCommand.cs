using MediatR;

namespace NotificationService.Application.Commands;

public record ProcessNotificationCommand(
    string EventType,
    string? Email,
    string? FullName,
    object Payload,
    string? CorrelationId = null,
    string? MessageId = null
) : IRequest;
