using Microsoft.EntityFrameworkCore;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}