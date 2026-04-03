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
    public string Type { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TraceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}