using MediatR;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NotificationService.Application.Commands;

public class ProcessNotificationCommandHandler(INotificationDbContext db, IEmailSender email, ILogger<ProcessNotificationCommandHandler> logger) : IRequestHandler<ProcessNotificationCommand>
{
    public async Task Handle(ProcessNotificationCommand request, CancellationToken ct)
    {
        var traceId = request.MessageId ?? Guid.NewGuid().ToString();
        var userId = ExtractUserId(request.Payload);

        logger.LogInformation("Processing {EventType} for {Email}, UserId={UserId}, TraceId={TraceId}", 
            request.EventType, request.Email, userId, traceId);

        db.AddAuditLog(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = request.EventType,
            EntityId = request.MessageId ?? "Event",
            Action = "Received",
            UserId = userId?.ToString() ?? request.Email ?? $"unknown-{request.EventType}",
            Changes = JsonConvert.SerializeObject(new { request.Payload, MessageId = request.MessageId, ReceivedAt = DateTime.UtcNow }),
            TraceId = traceId,
            CreatedAtUtc = DateTime.UtcNow
        });

        var notificationLog = new NotificationLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Recipient = request.Email ?? "N/A",
            Subject = request.EventType,
            Body = JsonConvert.SerializeObject(request.Payload),
            Type = "Event",
            IsSuccess = false,
            ErrorMessage = null,
            TraceId = traceId,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var (subject, body) = GenerateEmail(request.EventType, request.FullName, request.Payload);
            var (success, error) = await email.SendEmailAsync(request.Email, subject, body, ct);

            notificationLog.IsSuccess = success;
            notificationLog.Subject = success ? subject : request.EventType;
            notificationLog.Body = success ? body : JsonConvert.SerializeObject(request.Payload);
            notificationLog.ErrorMessage = error;

            db.AddAuditLog(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = request.EventType,
                EntityId = request.MessageId ?? "Event",
                Action = success ? "EmailSent" : "EmailFailed",
                UserId = userId?.ToString() ?? request.Email ?? "unknown",
                Changes = JsonConvert.SerializeObject(new { Subject = subject, Success = success, Error = error }),
                TraceId = traceId,
                CreatedAtUtc = DateTime.UtcNow
            });

            logger.LogInformation("Email {Status} for {Email}: {Subject}", success ? "sent" : "failed", request.Email, subject);
        }
        else
        {
            notificationLog.ErrorMessage = "No email provided";
            logger.LogWarning("No email for {EventType}", request.EventType);

            db.AddAuditLog(new AuditLog
            {
                Id = Guid.NewGuid(),
                EntityName = request.EventType,
                EntityId = request.MessageId ?? "Event",
                Action = "NoEmail",
                UserId = userId?.ToString() ?? $"unknown-{request.EventType}",
                Changes = JsonConvert.SerializeObject(new { Reason = "No email in event payload" }),
                TraceId = traceId,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        db.AddNotificationLog(notificationLog);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Completed {EventType}, UserId={UserId}, TraceId={TraceId}", request.EventType, userId, traceId);
    }

    private static Guid? ExtractUserId(object payload)
    {
        try
        {
            var json = JsonConvert.SerializeObject(payload);
            var obj = JObject.Parse(json);
            var userIdToken = obj["UserId"] ?? obj["userId"];
            if (userIdToken != null && Guid.TryParse(userIdToken.ToString(), out var userId))
                return userId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract UserId from payload: {ex.Message}");
        }
        return null;
    }

    private static (string Subject, string Body) GenerateEmail(string eventType, string? name, object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new();

        return eventType switch
        {
            "UserRegistered" => ("Welcome to CredVault", $"<h2>Welcome {name ?? "User"}!</h2><p>Your account is ready.</p>"),
            "UserOtpGenerated" => ("Your Verification Code", $"<h2>Code: {data.GetValueOrDefault("OtpCode", "N/A")}</h2><p>Purpose: {data.GetValueOrDefault("Purpose", "N/A")}</p>"),
            "CardAdded" => ("Card Added", $"<h2>New card ending in {data.GetValueOrDefault("CardNumberLast4", "****")}</h2>"),
            "BillGenerated" => ("Bill Ready", $"<h2>Amount: ₹{data.GetValueOrDefault("Amount", "0")}</h2><p>Due: {data.GetValueOrDefault("DueDate", "N/A")}</p>"),
            "PaymentOtpGenerated" => ("Payment Verification", $"<h2>Code: {data.GetValueOrDefault("OtpCode", "N/A")}</h2><p>Amount: ₹{data.GetValueOrDefault("Amount", "0")}</p>"),
            "PaymentCompleted" => ("Payment Successful", $"<h2>Payment of ₹{data.GetValueOrDefault("Amount", "0")} completed!</h2>"),
            "PaymentFailed" => ("Payment Failed", $"<h2>Payment failed: {data.GetValueOrDefault("Reason", "Unknown")}</h2>"),
            _ => (eventType, $"<h2>{eventType}</h2><p>Details: {json}</p>")
        };
    }
}
