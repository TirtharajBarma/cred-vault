using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs;
using Shared.Contracts.DTOs.Identity.Responses;
using Shared.Contracts.Events.Identity;
using MassTransit;
using MediatR;

namespace IdentityService.Application.Commands.Auth;

public record ResendVerificationCommand(string Email) : IRequest<OperationResult>;

public sealed class ResendVerificationCommandHandler(
    IUserRepository userRepository,
    IPublishEndpoint publishEndpoint)
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

        await publishEndpoint.Publish<IUserOtpGenerated>(new
        {
            user.Id,
            user.Email,
            user.FullName,
            OtpCode = otp,
            Purpose = "EmailVerification",
            ExpiresAtUtc = user.EmailVerificationOtpExpiresAtUtc
        }, cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Verification email sent."
        };
    }
}
