using IdentityService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs;
using Shared.Contracts.DTOs.Identity.Responses;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public record ResetPasswordCommand(string Email, string Otp, string NewPassword) : IRequest<OperationResult>;

public sealed class ResetPasswordCommandHandler(IUserRepository userRepository)
    : IRequestHandler<ResetPasswordCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Otp) || 
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email, OTP, and NewPassword are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.UserNotFound,
                Message = "No account exists with this email."
            };
        }

        if (string.IsNullOrWhiteSpace(user.PasswordResetOtp) || user.PasswordResetOtpExpiresAtUtc is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "No password reset request found. Please request a new OTP."
            };
        }

        if (user.PasswordResetOtpExpiresAtUtc <= DateTime.UtcNow)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.OtpExpired,
                Message = "OTP expired. Please request a new OTP."
            };
        }

        if (!string.Equals(user.PasswordResetOtp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "Invalid OTP."
            };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetOtp = null;
        user.PasswordResetOtpExpiresAtUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Password reset successfully."
        };
    }
}
