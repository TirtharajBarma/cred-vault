using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs;
using Shared.Contracts.DTOs.Identity.Responses;
using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Auth;

/// <summary>
/// Command to resend email verification OTP to user's email.
/// Used when user didn't receive original verification email or OTP expired.
/// </summary>
/// <param name="Email">User's registered email address</param>
public record ResendVerificationCommand(string Email) : IRequest<OperationResult>;

/// <summary>
/// Handler for ResendVerificationCommand:
/// 1. Validates email is provided
/// 2. Looks up user by email
/// 3. If user not found, returns error (unlike forgot-password, this reveals user exists)
/// 4. If email already verified, returns success (no need to resend)
/// 5. Generates new 6-digit OTP valid for 10 minutes
/// 6. Updates user's EmailVerificationOtp field
/// 7. Publishes IUserOtpGenerated event (NotificationService sends email)
/// </summary>
public sealed class ResendVerificationCommandHandler(IUserRepository users, IPublishEndpoint publisher, ILogger<ResendVerificationCommandHandler> logger) : IRequestHandler<ResendVerificationCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ResendVerificationCommand request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return new() { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Email required" };

        var user = await users.GetByEmailAsync(email, ct);
        if (user == null)
        {
            logger.LogWarning("Resend verification for non-existent email: {Email}", email);
            return new() { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found" };
        }

        if (user.IsEmailVerified)
        {
            logger.LogInformation("Resend verification for already verified email: {Email}", email);
            return new() { Success = true, Message = "Already verified" };
        }

        user.EmailVerificationOtp = IdentityHelpers.GenerateOtpCode();
        user.EmailVerificationOtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await users.UpdateAsync(user, ct);
            logger.LogInformation("OTP resent for {UserId}: {Email}", user.Id, email);

            await publisher.Publish<IUserOtpGenerated>(new { UserId = user.Id, user.Email, user.FullName, OtpCode = user.EmailVerificationOtp, Purpose = "EmailVerification", ExpiresAtUtc = user.EmailVerificationOtpExpiresAtUtc }, ct);
            logger.LogInformation("Published IUserOtpGenerated for {UserId}", user.Id);

            return new() { Success = true, Message = "OTP sent" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resend OTP for {Email}", email);
            return new() { Success = false, ErrorCode = "InternalError", Message = "Failed to resend OTP. Please try again." };
        }
    }
}
