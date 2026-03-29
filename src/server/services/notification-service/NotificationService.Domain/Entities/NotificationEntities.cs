using System;

namespace NotificationService.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? Changes { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class NotificationLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Email, SMS, etc.
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class EmailTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // UserRegistered, PaymentOTP, etc.
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
