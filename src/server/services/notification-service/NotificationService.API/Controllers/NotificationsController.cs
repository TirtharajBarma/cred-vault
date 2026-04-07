using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using NotificationService.Application.Queries.Notifications;
using Shared.Contracts.Controllers;

namespace NotificationService.API.Controllers;

[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator) : BaseApiController
{
    [HttpGet("logs")]
    public async Task<IActionResult> GetNotificationLogs([FromQuery] string? email, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetNotificationLogsQuery(email, page, pageSize), cancellationToken);
        return Ok(result);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? userId, [FromQuery] string? traceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetAuditLogsQuery(userId, traceId, page, pageSize), cancellationToken);
        return Ok(result);
    }
}