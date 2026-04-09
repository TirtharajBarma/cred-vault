using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Models;

namespace Shared.Contracts.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected ApiResponse<T> BuildResponse<T>(bool success, T data, string message)
    {
        return new ApiResponse<T>
        {
            Success = success,
            Message = message,
            Data = data,
            TraceId = HttpContext.TraceIdentifier
        };
    }

    protected IActionResult CreateResponse<T>(bool success, T data, string message, string? errorCode = null, int successStatusCode = StatusCodes.Status200OK)
    {
        var response = BuildResponse(success, data, message);

        if (success)
        {
            return StatusCode(successStatusCode, response);
        }

        return errorCode switch
        {
            "ValidationError" => BadRequest(response),
            "NotFound" or "UserNotFound" or "CardNotFound" => NotFound(response),
            "Conflict" or "DuplicateEmail" => Conflict(response),
            "Unauthorized" or "InvalidCredentials" => Unauthorized(response),
            "Forbidden" => StatusCode(StatusCodes.Status403Forbidden, response),
            "AccountLocked" => StatusCode(StatusCodes.Status403Forbidden, response),
            "ServiceUnavailable" => StatusCode(StatusCodes.Status503ServiceUnavailable, response),
            _ => BadRequest(response)
        };
    }

    protected Guid? GetUserIdFromToken()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }

    protected IActionResult UnauthorizedResponse(string message = "User identity is missing from token.")
    {
        return Unauthorized(new ApiResponse<object>
        {
            Success = false,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected IActionResult NotFoundResponse(string message)
    {
        return NotFound(new ApiResponse<object>
        {
            Success = false,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected IActionResult BadRequestResponse(string message)
    {
        return BadRequest(new ApiResponse<object>
        {
            Success = false,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

