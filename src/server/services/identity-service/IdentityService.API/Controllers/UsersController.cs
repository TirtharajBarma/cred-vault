using System.Security.Claims;
using IdentityService.Application.DTOs.Requests;
using IdentityService.Application.DTOs.Responses;
using Shared.Contracts.Models;
using IdentityService.Application.Commands.Users;
using IdentityService.Application.Queries.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/v1/identity/users")]
[Authorize]
public class UsersController : ControllerBase
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
        return FromUserResult(result);
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
        return FromUserResult(result);
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
        return FromUserResult(result);
    }

    [HttpPut("{userId:guid}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateUserStatus(Guid userId, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateUserStatusCommand(userId, request.Status), cancellationToken);
        return FromUserResult(result);
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

    private ApiResponse<T> BuildResponse<T>(bool success, T data, string message)
    {
        return new ApiResponse<T>
        {
            Success = success,
            Message = message,
            Data = data,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private Guid? GetUserIdFromToken()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }
}
