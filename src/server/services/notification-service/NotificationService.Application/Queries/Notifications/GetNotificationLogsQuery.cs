using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using Shared.Contracts.Models;

namespace NotificationService.Application.Queries.Notifications;

public record GetNotificationLogsQuery(string? Email, int Page, int PageSize) : IRequest<ApiResponse<GetNotificationLogsResult>>;

public record GetNotificationLogsResult(int Total, int Page, int PageSize, List<NotificationLog> Logs);

public class GetNotificationLogsQueryHandler(
    INotificationDbContext dbContext,
    ILogger<GetNotificationLogsQueryHandler> logger) : IRequestHandler<GetNotificationLogsQuery, ApiResponse<GetNotificationLogsResult>>
{
    public async Task<ApiResponse<GetNotificationLogsResult>> Handle(GetNotificationLogsQuery request, CancellationToken cancellationToken)
    {
        var query = ((INotificationDbContext)dbContext).NotificationLogs;

        if (!string.IsNullOrEmpty(request.Email))
        {
            query = query.Where(l => l.Recipient == request.Email);
        }

        var total = query.Count();
        var logs = query
            .OrderByDescending(l => l.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        logger.LogInformation("GetNotificationLogs: Email={Email}, Page={Page}, Total={Total}",
            request.Email, request.Page, total);

        return new ApiResponse<GetNotificationLogsResult>
        {
            Success = true,
            Message = "Notification logs fetched.",
            Data = new GetNotificationLogsResult(total, request.Page, request.PageSize, logs)
        };
    }
}
