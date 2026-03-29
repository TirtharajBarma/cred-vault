using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MediatR;
using MassTransit;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Auth;

public record ForgotPasswordCommand(string Email) : IRequest<OperationResult>;

public sealed class ForgotPasswordCommandHandler(
    IUserRepository userRepository,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<ForgotPasswordCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
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
                Success = true,
                Message = "If an account exists with this email, a password reset OTP has been sent."
            };
        }

        var otp = IdentityHelpers.GenerateOtpCode();
        var now = DateTime.UtcNow;

        user.PasswordResetOtp = otp;
        user.PasswordResetOtpExpiresAtUtc = now.AddMinutes(10);
        user.UpdatedAtUtc = now;

        await userRepository.UpdateAsync(user, cancellationToken);

        await publishEndpoint.Publish<IUserOtpGenerated>(new
        {
            user.Id,
            user.Email,
            user.FullName,
            OtpCode = otp,
            Purpose = "PasswordReset",
            ExpiresAtUtc = user.PasswordResetOtpExpiresAtUtc
        }, cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "If an account exists with this email, a password reset OTP has been sent."
        };
    }
}
