using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Enums;
using MediatR;
using Shared.Contracts.DTOs;

namespace IdentityService.Application.Queries.Users;

/// <summary>
/// Query to list all users with pagination and optional search/filter.
/// Used by admin to view and manage users.
/// </summary>
/// <param name="Page">Page number (default 1)</param>
/// <param name="PageSize">Items per page (default 20)</param>
/// <param name="Search">Search by email or full name</param>
/// <param name="Status">Filter by status (active, blocked, suspended, pendingverification)</param>
public sealed record ListAllUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Status = null
) : IRequest<OperationResult>;

/// <summary>
/// Handler for ListAllUsersQuery:
/// 1. Parses optional status filter from string to UserStatus enum
/// 2. Calls repository ListAllAsync with pagination and filters
/// 3. Maps results to simplified DTO with id, email, fullName, role, status, createdAtUtc
/// 4. Returns paginated result with total count
/// </summary>
public sealed class ListAllUsersQueryHandler(IUserRepository userRepository)
    : IRequestHandler<ListAllUsersQuery, OperationResult>
{
    public async Task<OperationResult> Handle(ListAllUsersQuery request, CancellationToken ct)
    {
        UserStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<UserStatus>(request.Status, true, out var status))
        {
            statusFilter = status;
        }

        var (users, totalCount) = await userRepository.ListAllAsync(
            request.Page,
            request.PageSize,
            request.Search,
            statusFilter,
            ct);

        var userList = users.Select(u => new
        {
            id = u.Id,
            email = u.Email,
            fullName = u.FullName,
            role = u.Role == Domain.Enums.UserRole.Admin ? "admin" : "user",
            status = u.Status.ToString().ToLower(),
            createdAtUtc = u.CreatedAtUtc
        }).ToList();

        return new OperationResult
        {
            Success = true,
            Data = new
            {
                total = totalCount,
                page = request.Page,
                pageSize = request.PageSize,
                users = userList
            }
        };
    }
}