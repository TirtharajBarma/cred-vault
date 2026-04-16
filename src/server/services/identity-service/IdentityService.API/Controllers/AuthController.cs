using Shared.Contracts.Controllers;
using Shared.Contracts.DTOs.Identity.Requests;
using IdentityService.Application.Commands.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

/// <summary>
/// Authentication controller handling all user authentication and account management endpoints.
/// Provides endpoints for user registration, login, password management, and email verification.
/// Uses MediatR pattern to delegate business logic to handlers in the Application layer.
/// All responses follow the standard ApiResponse format with success status and message.
/// </summary>
/// <remarks>
/// Endpoints:
/// - POST /register: Create new user account
/// - POST /login: Authenticate with email/password
/// - POST /google: Authenticate using Google OAuth (SSO)
/// - POST /resend-verification: Resend email verification OTP
/// - POST /verify-email-otp: Verify email with OTP
/// - POST /forgot-password: Request password reset
/// - POST /reset-password: Reset password using OTP
/// </remarks>
[Route("api/v1/identity/auth")]
public class AuthController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// Register a new user account.
    /// </summary>
    /// <param name="request">RegisterRequest containing Email, Password, FullName</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult with access token and user data if successful</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        // register -> DTO
        var result = await mediator.Send(new RegisterCommand(request.Email, request.Password, request.FullName), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode, StatusCodes.Status201Created);
    }

    /// <summary>
    /// Login with email and password credentials.
    /// </summary>
    /// <param name="request">LoginRequest containing Email, Password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult with access token and user data if credentials are valid</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Login/Register using Google OAuth. Automatically creates account if not exists.
    /// </summary>
    /// <param name="request">GoogleLoginRequest containing Google IdToken</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult with access token and user data</returns>
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GoogleLoginCommand(request.IdToken), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Resend email verification OTP to user's email.
    /// </summary>
    /// <param name="request">ResendVerificationRequest containing Email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating success or failure</returns>
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResendVerificationCommand(request.Email), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Verify user's email using one-time password (OTP) sent to their email.
    /// </summary>
    /// <param name="request">VerifyEmailOtpRequest containing Email and Otp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AuthResult with access token if OTP is valid</returns>
    [HttpPost("verify-email-otp")]
    public async Task<IActionResult> VerifyEmailOtp([FromBody] VerifyEmailOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new VerifyEmailOtpCommand(request.Email, request.Otp), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Request password reset. Sends OTP to registered email.
    /// </summary>
    /// <param name="request">ForgotPasswordRequest containing Email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating email was sent</returns>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ForgotPasswordCommand(request.Email), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Reset password using OTP sent to email.
    /// </summary>
    /// <param name="request">ResetPasswordRequest containing Email, Otp, NewPassword</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating password was reset</returns>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ResetPasswordCommand(request.Email, request.Otp, request.NewPassword), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }
}

