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
        logger.LogInformation("Processing notification: {EventType} for {Email}", request.EventType, request.Email ?? "NO_EMAIL");

        // 1. Audit the Business Event (use fallback for UserId if email is null)
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = request.EventType,
            EntityId = "Event",
            Action = "Consumed",
            UserId = request.Email ?? $"unknown-{request.EventType}",
            Changes = JsonConvert.SerializeObject(request.Payload),
            TraceId = request.TraceId,
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.AuditLogs.Add(audit);

        // 2. Skip email if email is not provided
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            logger.LogWarning("No email provided for event {EventType}. Skipping email notification.", request.EventType);
            
            var skippedLog = new NotificationLog
            {
                Id = Guid.NewGuid(),
                Recipient = "N/A",
                Subject = $"Skipped: {request.EventType}",
                Body = "Email not provided in event payload",
                Type = "Email",
                IsSuccess = false,
                ErrorMessage = "Email was null or empty in the event",
                TraceId = request.TraceId,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.NotificationLogs.Add(skippedLog);
            
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // 3. Fetch Notification Template
        var template = await dbContext.EmailTemplates
            .FirstOrDefaultAsync(t => t.Name == request.EventType, cancellationToken);

        if (template is null)
        {
            logger.LogWarning("No email template found for event type: {EventType}", request.EventType);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // 4. Render Template
        var subject = RenderTemplate(template.SubjectTemplate, request.FullName ?? "User", request.Payload);
        var body = RenderTemplate(template.BodyTemplate, request.FullName ?? "User", request.Payload);

        // 5. Send Email
        var (success, error) = await emailSender.SendEmailAsync(request.Email, subject, body, cancellationToken);

        // 6. Log Notification
        var notifLog = new NotificationLog
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
        dbContext.NotificationLogs.Add(notifLog);

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
