using Shared.Contracts.Controllers;
using Shared.Contracts.DTOs.Identity.Requests;
using IdentityService.Application.Commands.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[Route("api/v1/identity/auth")]
public class AuthController(IMediator mediator) : BaseApiController
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RegisterCommand(request.Email, request.Password, request.FullName), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode, StatusCodes.Status201Created);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResendVerificationCommand(request.Email), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    [HttpPost("verify-email-otp")]
    public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new VerifyEmailOtpCommand(request.Email, request.Otp), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResetPasswordCommand(request.Email, request.Otp, request.NewPassword), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }
}

