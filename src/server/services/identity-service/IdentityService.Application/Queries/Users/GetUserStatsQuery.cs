using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Enums;
using MediatR;
using Shared.Contracts.DTOs;

namespace IdentityService.Application.Queries.Users;

/// <summary>
/// Query to get user statistics (counts by status).
/// Used by admin dashboard to display user metrics.
/// </summary>
public sealed record GetUserStatsQuery : IRequest<OperationResult>;

/// <summary>
/// Handler for GetUserStatsQuery:
/// 1. Calls repository GetCountByStatusAsync to get user counts grouped by status
/// 2. Calculates total users from sum of all status counts
/// 3. Returns breakdown: totalUsers, activeUsers, pendingUsers, suspendedUsers, blockedUsers
/// </summary>
public sealed class GetUserStatsQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserStatsQuery, OperationResult>
{
    public async Task<OperationResult> Handle(GetUserStatsQuery request, CancellationToken ct)
    {
        var statusCounts = await userRepository.GetCountByStatusAsync(ct);

        var totalUsers = statusCounts.Values.Sum();
        var activeUsers = statusCounts.GetValueOrDefault(UserStatus.Active, 0);
        var pendingUsers = statusCounts.GetValueOrDefault(UserStatus.PendingVerification, 0);
        var suspendedUsers = statusCounts.GetValueOrDefault(UserStatus.Suspended, 0);
        var blockedUsers = statusCounts.GetValueOrDefault(UserStatus.Blocked, 0);

        return new OperationResult
        {
            Success = true,
            Data = new
            {
                totalUsers,
                activeUsers,
                pendingUsers,
                suspendedUsers,
                blockedUsers
            }
        };
    }
}