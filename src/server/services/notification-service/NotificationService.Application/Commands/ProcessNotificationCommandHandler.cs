 using MediatR;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Application.Services;
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
        var payloadDict = DeserializePayload(request.Payload);      // convert payload -> dictionary

        logger.LogInformation("Processing {EventType} for {Email}, UserId={UserId}, TraceId={TraceId}", 
            request.EventType, request.Email, userId, traceId);

        // request.EventType -> "UserRegistered", "PaymentCompleted", "OtpFailed"

        db.AddAuditLog(new AuditLog
        {
            Id = Guid.NewGuid(),
            EntityName = request.EventType,
            EntityId = request.MessageId ?? "Event",
            Action = "Received",
            UserId = userId?.ToString() ?? request.Email ?? $"unknown-{request.EventType}",     // priority -> UserId, Email, fallback string
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
            var (subject, body) = GenerateEmail(request.EventType, request.FullName ?? "User", payloadDict);
            var (success, error) = await email.SendEmailAsync(request.Email, subject, body, ct);        // send email

            notificationLog.IsSuccess = success;
            notificationLog.Subject = success ? subject : request.EventType;        // if email access -> use real subject ELSE fallback to event type
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
            if (userIdToken != null && Guid.TryParse(userIdToken.ToString(), out var userId))       // safe parsing
                return userId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract UserId from payload: {ex.Message}");
        }
        return null;
    }

    private static Dictionary<string, object> DeserializePayload(object payload)
    {
        try
        {
            var json = JsonConvert.SerializeObject(payload);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new();        // JSON -> dictionary
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static string GetString(Dictionary<string, object> data, string key, string defaultValue = "")
    {
        if (data.TryGetValue(key, out var value))       // Type safe if key is not there
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static decimal GetDecimal(Dictionary<string, object> data, string key, decimal defaultValue = 0)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (decimal.TryParse(value?.ToString(), out var result))
                return result;
        }
        return defaultValue;
    }

    private static DateTime GetDateTime(Dictionary<string, object> data, string key, DateTime defaultValue = default)
    {
        if (data.TryGetValue(key, out var value))
        {
            if (DateTime.TryParse(value?.ToString(), out var result))
                return result;
        }
        return defaultValue;
    }

    private static (string Subject, string Body) GenerateEmail(string eventType, string fullName, Dictionary<string, object> data)
    {
        return eventType switch
        {
            "UserRegistered" => (
                "Welcome to CredVault! 🎉",
                EmailTemplates.UserWelcome(fullName, GetString(data, "email"))
            ),

            "UserOtpGenerated" => (
                GetString(data, "Purpose") switch
                {
                    "EmailVerification" => "Verify Your Email Address 📧",
                    "PasswordReset" => "Password Reset Code 🔑",
                    _ => "Your Verification Code"
                },
                GetString(data, "Purpose") switch
                {
                    "PasswordReset" => EmailTemplates.PasswordResetOtp(
                        fullName,
                        GetString(data, "OtpCode"),
                        GetDateTime(data, "ExpiresAtUtc")
                    ),
                    _ => EmailTemplates.EmailVerificationOtp(
                        fullName,
                        GetString(data, "OtpCode"),
                        GetString(data, "Purpose"),
                        GetDateTime(data, "ExpiresAtUtc")
                    )
                }
            ),

            "CardAdded" => (
                "New Card Added to Your Account 💳",
                EmailTemplates.CardAdded(
                    fullName,
                    GetString(data, "CardNumberLast4"),
                    GetString(data, "CardHolderName"),
                    GetDateTime(data, "AddedAt")
                )
            ),

            "BillGenerated" => (
                $"Your Bill is Ready — ₹{GetDecimal(data, "Amount"):N2} Due",
                EmailTemplates.BillGenerated(
                    fullName,
                    GetDecimal(data, "Amount"),
                    GetDateTime(data, "DueDate"),
                    GetString(data, "BillId")
                )
            ),

            "PaymentOtpGenerated" => (
                "Payment Verification Required 🔐",
                EmailTemplates.PaymentOtp(
                    fullName,
                    GetDecimal(data, "Amount"),
                    GetString(data, "OtpCode"),
                    GetDateTime(data, "ExpiresAtUtc")
                )
            ),

            "PaymentCompleted" => (
                "✅ Payment Successful!",
                EmailTemplates.PaymentCompleted(
                    fullName,
                    GetDecimal(data, "Amount"),
                    GetDecimal(data, "AmountPaid", GetDecimal(data, "Amount")),
                    GetDecimal(data, "RewardsRedeemed", 0),
                    GetString(data, "PaymentId")
                )
            ),

            "PaymentFailed" => (
                "❌ Payment Failed",
                EmailTemplates.PaymentFailed(
                    fullName,
                    GetDecimal(data, "Amount"),
                    GetString(data, "Reason", "Unknown error occurred"),
                    GetString(data, "PaymentId")
                )
            ),

            "OtpFailed" => (
                "⚠️ OTP Verification Failed",
                EmailTemplates.OtpVerificationFailed(
                    fullName,
                    GetString(data, "Reason", "Verification could not be completed")
                )
            ),

            _ => (
                eventType,
                EmailTemplates.BaseTemplate(
                    eventType,
                    "You have a new notification from CredVault",
                    $@"<p style=""margin: 0; color: #1F2937; font-size: 16px;"">
                        Hello <strong>{fullName}</strong>,
                    </p>
                    <p style=""margin: 24px 0; color: #1F2937; font-size: 16px;"">
                        {eventType}
                    </p>"
                )
            )
        };
    }
}
