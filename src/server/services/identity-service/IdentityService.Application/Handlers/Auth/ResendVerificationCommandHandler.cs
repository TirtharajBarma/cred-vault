using IdentityService.Application.Abstractions.Notifications;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Auth;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Handlers.Auth;

public sealed class ResendVerificationCommandHandler(
    IUserRepository userRepository,
    IEmailSender emailSender)
    : IRequestHandler<ResendVerificationCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email is required."
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

        var otp = IdentityHelpers.GenerateOtpCode();
        user.EmailVerificationOtp = otp;
        user.EmailVerificationOtpExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        try
        {
            await emailSender.SendAsync(
                user.Email,
                "CredVault verification code",
                $"Your verification OTP is {otp}. It will expire in 10 minutes.",
                cancellationToken);
        }
        catch
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.EmailSendFailed,
                Message = "Failed to send verification email. Check SMTP configuration."
            };
        }

        return new OperationResult
        {
            Success = true,
            Message = "Verification email sent."
        };
    }
}
