using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;
using MassTransit;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Auth;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResult>;

public sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IPublishEndpoint publishEndpoint,
    IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<RegisterCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "FullName, Email and Password are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.DuplicateEmail,
                Message = "Email already registered."
            };
        }

        var otp = IdentityHelpers.GenerateOtpCode();
        var now = DateTime.UtcNow;

        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            EmailVerificationOtp = otp,
            EmailVerificationOtpExpiresAtUtc = now.AddMinutes(10),
            Status = UserStatus.PendingVerification,
            Role = UserRole.User,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await userRepository.AddAsync(user, cancellationToken);

        await publishEndpoint.Publish<IUserOtpGenerated>(new
        {
            user.Id,
            user.Email,
            user.FullName,
            OtpCode = otp,
            Purpose = "EmailVerification",
            ExpiresAtUtc = user.EmailVerificationOtpExpiresAtUtc
        }, cancellationToken);

        var accessToken = IdentityHelpers.GenerateAccessToken(user, jwtOptions.Value);

        return new AuthResult
        {
            Success = true,
            Message = "Registration successful. Please verify your email.",
            AccessToken = accessToken,
            User = IdentityHelpers.ToUserSummary(user)
        };
    }
}
