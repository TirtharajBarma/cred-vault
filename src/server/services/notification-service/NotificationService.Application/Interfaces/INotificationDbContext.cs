using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationDbContext
{
    IQueryable<AuditLog> AuditLogs { get; }
    IQueryable<NotificationLog> NotificationLogs { get; }

    void AddAuditLog(AuditLog log);
    void AddNotificationLog(NotificationLog log);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}