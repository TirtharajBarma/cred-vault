using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs;
using Shared.Contracts.DTOs.Identity.Responses;
using MediatR;
using Microsoft.Extensions.Logging;
using MassTransit;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Auth;

public record ForgotPasswordCommand(string Email) : IRequest<OperationResult>;

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

            await publisher.Publish(new { user.Id, user.Email, user.FullName, OtpCode = otp, Purpose = "PasswordReset", ExpiresAtUtc = user.PasswordResetOtpExpiresAtUtc }, ct);
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
