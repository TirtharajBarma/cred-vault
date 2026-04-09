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
/// Command to request password reset for a user.
/// Generates OTP and sends to user's registered email.
/// </summary>
/// <param name="Email">User's registered email address</param>
public record ForgotPasswordCommand(string Email) : IRequest<OperationResult>;

/// <summary>
/// Handler for ForgotPasswordCommand:
/// 1. Validates email is provided
/// 2. Looks up user by email - if not found, returns success (prevents email enumeration)
/// 3. Generates 6-digit OTP valid for 10 minutes
/// 4. Stores OTP in user's PasswordResetOtp field
/// 5. Publishes IUserOtpGenerated event (NotificationService sends email)
/// 6. Returns generic message "If exists, OTP sent" for security
/// </summary>
public sealed class ForgotPasswordCommandHandler(IUserRepository users, IPublishEndpoint publisher, ILogger<ForgotPasswordCommandHandler> logger) : IRequestHandler<ForgotPasswordCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return new() { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Email required" };

        var user = await users.GetByEmailAsync(email, ct);
        if (user == null)
        {
            logger.LogWarning("Password reset requested for non-existent email: {Email}", email);
            return new() { Success = true, Message = "If exists, OTP sent" };
        }

        var otp = IdentityHelpers.GenerateOtpCode();
        user.PasswordResetOtp = otp;
        user.PasswordResetOtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await users.UpdateAsync(user, ct);
            logger.LogInformation("Password reset OTP generated for {UserId}: {Email}", user.Id, email);

            await publisher.Publish<IUserOtpGenerated>(new { UserId = user.Id, user.Email, user.FullName, OtpCode = otp, Purpose = "PasswordReset", ExpiresAtUtc = user.PasswordResetOtpExpiresAtUtc }, ct);
            logger.LogInformation("Published IUserOtpGenerated for {UserId}", user.Id);

            return new() { Success = true, Message = "If exists, OTP sent" };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process password reset for {Email}", email);
            return new() { Success = false, ErrorCode = "InternalError", Message = "Password reset failed. Please try again." };
        }
    }
}
