using System.Security.Claims;
using Shared.Contracts.DTOs.Identity.Requests;
using Shared.Contracts.DTOs.Identity.Responses;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;
using Shared.Contracts.DTOs;
using IdentityService.Application.Commands.Users;
using IdentityService.Application.Queries.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

/// <summary>
/// User management controller providing endpoints for user profile operations and admin user management.
/// Handles both user-level operations (get/update own profile) and admin-level operations (manage all users).
/// Uses JWT-based authentication via Bearer tokens in the Authorization header.
/// </summary>
/// <remarks>
/// User endpoints (requires authentication):
/// - GET /me: Get current user's profile
/// - PUT /me: Update current user's profile (full name)
/// - PUT /me/password: Change current user's password
///
/// Admin endpoints (requires admin role):
/// - GET /{userId}: Get user by ID
/// - PUT /{userId}/status: Update user's account status
/// - PUT /{userId}/role: Update user's role
/// - GET /: List all users with pagination
/// - GET /stats: Get user statistics
/// </remarks>
[ApiController]
[Route("api/v1/identity/users")]
[Authorize]
public class UsersController : BaseApiController
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get current authenticated user's profile information.
    /// Extracts user ID from JWT token in Authorization header.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult containing user details (id, email, fullName, role, status)</returns>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "User identity is missing from token.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var result = await _mediator.Send(new GetMeQuery(userId.Value), cancellationToken);
        return FromAuthResult(result);
    }

    /// <summary>
    /// Update current user's profile (full name only).
    /// Requires authentication - uses JWT token to identify the user.
    /// </summary>
    /// <param name="request">UpdateProfileRequest containing FullName to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult with updated user info and new access token</returns>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "User identity is missing from token.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var result = await _mediator.Send(new UpdateMeCommand(userId.Value, request.FullName), cancellationToken);
        return FromAuthResult(result);
    }

    /// <summary>
    /// Change current user's password. Requires current password for verification.
    /// </summary>
    /// <param name="request">ChangePasswordRequest with CurrentPassword and NewPassword</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating success or failure</returns>
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeMyPassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "User identity is missing from token.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var result = await _mediator.Send(new ChangeMyPasswordCommand(userId.Value, request.CurrentPassword, request.NewPassword), cancellationToken);
        return FromOperationResult(result);
    }

    /// <summary>
    /// Get any user's profile by their unique ID. Admin only endpoint.
    /// </summary>
    /// <param name="userId">The unique GUID of the user to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult containing user details</returns>
    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUserById(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken);
        return FromAuthResult(result);
    }

    /// <summary>
    /// Update user's account status (active, suspended, blocked, etc). Admin only.
    /// </summary>
    /// <param name="userId">User's unique ID</param>
    /// <param name="request">UpdateUserStatusRequest with Status (active, suspended, blocked, pendingverification)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating success</returns>
    [HttpPut("{userId:guid}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<IdentityService.Domain.Enums.UserStatus>(request.Status, true, out var status))
        {
            return BadRequest(BuildResponse(false, (object?)null, "Invalid status value."));
        }

        var result = await _mediator.Send(new UpdateUserStatusCommand(userId, status), cancellationToken);
        return FromOperationResult(result);
    }

    /// <summary>
    /// Update user's role (user or admin). Admin only.
    /// </summary>
    /// <param name="userId">User's unique ID</param>
    /// <param name="request">UpdateUserRoleRequest with Role (user or admin)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult with updated role message</returns>
    [HttpPut("{userId:guid}/role")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        var roleStr = request.Role?.Trim().ToLowerInvariant() ?? "";
        var role = roleStr switch
        {
            "admin" => IdentityService.Domain.Enums.UserRole.Admin,
            "user" => IdentityService.Domain.Enums.UserRole.User,
            _ => (IdentityService.Domain.Enums.UserRole?)null
        };

        if (role is null)
        {
            return BadRequest(BuildResponse(false, (object?)null, "Invalid role value. Use 'admin' or 'user'."));
        }

        var result = await _mediator.Send(new UpdateUserRoleCommand(userId, role.Value), cancellationToken);
        return FromOperationResult(result);
    }

    /// <summary>
    /// List all users with pagination and optional search/filter. Admin only.
    /// </summary>
    /// <param name="page">Page number (default 1)</param>
    /// <param name="pageSize">Items per page (default 20)</param>
    /// <param name="search">Search by email or full name</param>
    /// <param name="status">Filter by status (active, blocked, etc)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult with paginated list of users and total count</returns>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> ListAllUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListAllUsersQuery(page, pageSize, search, status), cancellationToken);
        return FromOperationResult(result);
    }

    /// <summary>
    /// Get user statistics (counts by status). Admin only.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult with dictionary of status counts</returns>
    [HttpGet("stats")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUserStats(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserStatsQuery(), cancellationToken);
        return FromOperationResult(result);
    }

    private IActionResult FromAuthResult(AuthResult result)
    {
        var response = BuildResponse(result.Success, new { result.User, result.AccessToken }, result.Message);

        if (result.Success)
        {
            return Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.UserNotFound => NotFound(response),
            ErrorCodes.InvalidCredentials => Unauthorized(response),
            ErrorCodes.AccountLocked => BadRequest(response),
            ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }

    private IActionResult FromOperationResult(OperationResult result)
    {
        var response = BuildResponse(result.Success, result, result.Message);

        if (result.Success)
        {
            return Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.InvalidCredentials => Unauthorized(response),
            ErrorCodes.UserNotFound => NotFound(response),
            ErrorCodes.ValidationError => BadRequest(response),
            _ => BadRequest(response)
        };
    }

}
