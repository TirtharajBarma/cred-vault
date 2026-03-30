using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Enums;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Commands.Auth;

public record VerifyEmailOtpCommand(string Email, string Otp) : IRequest<AuthResult>;

public sealed class VerifyEmailOtpCommandHandler(IUserRepository userRepository, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<VerifyEmailOtpCommand, AuthResult>
{
    public async Task<AuthResult> Handle(VerifyEmailOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email and OTP are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user is null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.UserNotFound,
                Message = "No account exists with this email."
            };
        }

        if (user.IsEmailVerified)
        {
            return new AuthResult
            {
                Success = true,
                Message = "Email is already verified.",
                AccessToken = IdentityHelpers.GenerateAccessToken(user, jwtOptions.Value),
                User = IdentityHelpers.ToUserSummary(user)
            };
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationOtp) || user.EmailVerificationOtpExpiresAtUtc is null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "OTP not found. Please request a new OTP."
            };
        }

        if (user.EmailVerificationOtpExpiresAtUtc <= DateTime.UtcNow)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.OtpExpired,
                Message = "OTP expired. Please request a new OTP."
            };
        }

        if (!string.Equals(user.EmailVerificationOtp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "Invalid OTP."
            };
        }

        user.IsEmailVerified = true;
        user.Status = UserStatus.Active;
        user.EmailVerificationOtp = null;
        user.EmailVerificationOtpExpiresAtUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        // Generate Access Token for direct login
        var accessToken = IdentityHelpers.GenerateAccessToken(user, jwtOptions.Value);

        return new AuthResult
        {
            Success = true,
            Message = "Email verified successfully.",
            AccessToken = accessToken,
            User = IdentityHelpers.ToUserSummary(user)
        };
    }
}
