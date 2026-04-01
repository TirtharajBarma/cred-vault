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

    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUserById(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserByIdQuery(userId), cancellationToken);
        return FromAuthResult(result);
    }

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

    [HttpGet("stats")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetUserStats(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetUserStatsQuery(), cancellationToken);
        return FromOperationResult(result);
    }

    private IActionResult FromUserResult(UserResult result)
    {
        var response = BuildResponse(result.Success, result.User, result.Message);

        if (result.Success)
        {
            return Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.UserNotFound => NotFound(response),
            ErrorCodes.InvalidStatus => BadRequest(response),
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
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
