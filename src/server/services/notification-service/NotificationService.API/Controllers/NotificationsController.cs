using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Infrastructure.Persistence;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace NotificationService.API.Controllers;

[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(NotificationDbContext dbContext) : BaseApiController
{
    [HttpGet("logs")]
    public async Task<IActionResult> GetNotificationLogs([FromQuery] string? email, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = dbContext.NotificationLogs.AsQueryable();

        if (!string.IsNullOrEmpty(email))
        {
            query = query.Where(l => l.Recipient == email);
        }

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return CreateResponse(true, new { total, page, pageSize, logs }, "Notification logs fetched.");
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? traceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = dbContext.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(traceId))
        {
            query = query.Where(l => l.TraceId == traceId);
        }

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return CreateResponse(true, new { total, page, pageSize, logs }, "Audit logs fetched.");
    }
}