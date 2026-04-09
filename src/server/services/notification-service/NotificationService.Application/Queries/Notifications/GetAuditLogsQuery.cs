using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using Shared.Contracts.Models;

namespace NotificationService.Application.Queries.Notifications;

public record GetAuditLogsQuery(string? UserId, string? TraceId, int Page, int PageSize) : IRequest<ApiResponse<GetAuditLogsResult>>;

public record GetAuditLogsResult(int Total, int Page, int PageSize, List<AuditLog> Logs);

public class GetAuditLogsQueryHandler(
    INotificationDbContext dbContext,
    ILogger<GetAuditLogsQueryHandler> logger) : IRequestHandler<GetAuditLogsQuery, ApiResponse<GetAuditLogsResult>>
{
    public async Task<ApiResponse<GetAuditLogsResult>> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = ((INotificationDbContext)dbContext).AuditLogs;

        if (!string.IsNullOrEmpty(request.UserId))
        {
            query = query.Where(l => l.UserId == request.UserId);
        }

        if (!string.IsNullOrEmpty(request.TraceId))
        {
            query = query.Where(l => l.TraceId == request.TraceId);
        }

        var total = query.Count();
        var logs = query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        logger.LogInformation("GetAuditLogs: UserId={UserId}, TraceId={TraceId}, Page={Page}, Total={Total}",
            request.UserId, request.TraceId, request.Page, total);

        return new ApiResponse<GetAuditLogsResult>
        {
            Success = true,
            Message = "Audit logs fetched.",
            Data = new GetAuditLogsResult(total, request.Page, request.PageSize, logs)
        };
    }
}
