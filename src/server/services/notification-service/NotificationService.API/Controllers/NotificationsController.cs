using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NotificationService.Application.Queries.Notifications;
using Shared.Contracts.Controllers;

namespace NotificationService.API.Controllers;

/// <summary>
/// Notification controller for viewing notification logs and audit trails.
/// Provides admin access to notification history for debugging and compliance.
/// </summary>
/// <remarks>
/// Admin endpoints (requires admin role):
/// - GET /logs: View notification logs (sent emails, SMS, etc)
/// - GET /audit: View audit logs of all notification operations
/// </remarks>
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// Admin: Get notification logs (emails sent, SMS, etc).
    /// Supports filtering by email and pagination.
    /// </summary>
    /// <param name="email">Optional email filter</param>
    /// <param name="page">Page number (default 1)</param>
    /// <param name="pageSize">Items per page (default 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with paginated notification logs</returns>
    [HttpGet("logs")]
    public async Task<IActionResult> GetNotificationLogs([FromQuery] string? email, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetNotificationLogsQuery(email, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin: Get audit logs for all notification operations.
    /// Useful for debugging notification delivery issues.
    /// </summary>
    /// <param name="userId">Optional user ID filter</param>
    /// <param name="traceId">Optional trace ID filter</param>
    /// <param name="page">Page number (default 1)</param>
    /// <param name="pageSize">Items per page (default 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with paginated audit logs</returns>
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? userId, [FromQuery] string? traceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAuditLogsQuery(userId, traceId, page, pageSize), cancellationToken);
        return Ok(result);
    }
}