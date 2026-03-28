using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public record VerifyEmailOtpCommand(string Email, string Otp) : IRequest<OperationResult>;

public sealed class VerifyEmailOtpCommandHandler(IUserRepository userRepository)
    : IRequestHandler<VerifyEmailOtpCommand, OperationResult>
{
    public async Task<OperationResult> Handle(VerifyEmailOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Otp))
        {
            return new OperationResult
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
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.UserNotFound,
                Message = "No account exists with this email."
            };
        }

        if (user.IsEmailVerified)
        {
            return new OperationResult
            {
                Success = true,
                Message = "Email is already verified."
            };
        }

        if (string.IsNullOrWhiteSpace(user.EmailVerificationOtp) || user.EmailVerificationOtpExpiresAtUtc is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidOtp,
                Message = "OTP not found. Please request a new OTP."
            };
        }

        if (user.EmailVerificationOtpExpiresAtUtc <= DateTime.UtcNow)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.OtpExpired,
                Message = "OTP expired. Please request a new OTP."
            };
        }

        if (!string.Equals(user.EmailVerificationOtp, request.Otp.Trim(), StringComparison.Ordinal))
        {
            return new OperationResult
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

        return new OperationResult
        {
            Success = true,
            Message = "Email verified successfully."
        };
    }
}
