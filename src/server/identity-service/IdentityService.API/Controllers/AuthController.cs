using IdentityService.API.Models;
using IdentityService.Application.Commands.Auth;
using IdentityService.Application.Queries.Auth;
using IdentityService.Application.DTOs.Requests;
using IdentityService.Application.DTOs.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/v1/identity/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RegisterCommand(request.FullName, request.Email, request.Password), cancellationToken);
        return FromAuthResult(result, StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginQuery(request.Email, request.Password), cancellationToken);
        return FromAuthResult(result, StatusCodes.Status200OK);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ResendVerificationCommand(request.Email), cancellationToken);
        return FromOperationResult(result);
    }

    [HttpPost("verify-email-otp")]
    public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new VerifyEmailOtpCommand(request.Email, request.Otp), cancellationToken);
        return FromOperationResult(result);
    }

    private IActionResult FromAuthResult(AuthResult result, int successStatusCode)
    {
        var response = BuildResponse(result.Success, result, result.Message);

        if (result.Success)
        {
            return StatusCode(successStatusCode, response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.DuplicateEmail => Conflict(response),
            ErrorCodes.InvalidCredentials => Unauthorized(response),
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
            ErrorCodes.UserNotFound => NotFound(response),
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.InvalidOtp => BadRequest(response),
            ErrorCodes.OtpExpired => BadRequest(response),
            ErrorCodes.EmailSendFailed => StatusCode(StatusCodes.Status503ServiceUnavailable, response),
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
}
