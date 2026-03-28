using MediatR;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Commands;

using Consumers;

public class ProcessNotificationCommandHandler(
    INotificationDbContext dbContext,
    IEmailSender emailSender,
    ILogger<ProcessNotificationCommandHandler> logger)
    : IRequestHandler<ProcessNotificationCommand>
{
    public async Task Handle(ProcessNotificationCommand request, CancellationToken cancellationToken)
    {
        // 1. Audit the Business Event
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = request.EventType,
            EntityId = "Event",
            Action = "Consumed",
            UserId = request.Email, // Store email as UserId if real ID isn't available
            Changes = JsonConvert.SerializeObject(request.Payload),
            TraceId = request.TraceId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.AuditLogs.Add(audit);

        // 2. Fetch Notification Template
        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Name == request.EventType, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("No email template found for event type: {EventType}", request.EventType);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // 3. Render Template
        var subject = RenderTemplate(template.SubjectTemplate, request.FullName, request.Payload);
        var body = RenderTemplate(template.BodyTemplate, request.FullName, request.Payload);

        // 4. Send Email
        var (success, error) = await emailSender.SendEmailAsync(request.Email, subject, body, cancellationToken);

        // 5. Log Notification
        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            Recipient = request.Email,
            Subject = subject,
            Body = body,
            Type = "Email",
            IsSuccess = success,
            ErrorMessage = error,
            TraceId = request.TraceId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.NotificationLogs.Add(log);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private string RenderTemplate(string template, string fullName, object payload)
    {
        var result = template.Replace("{{FullName}}", fullName, StringComparison.OrdinalIgnoreCase);
        
        // Use reflection or JSON to replace other variables
        var json = JsonConvert.SerializeObject(payload);
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

        if (dict != null)
        {
            foreach (var kvp in dict)
            {
                result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
}
