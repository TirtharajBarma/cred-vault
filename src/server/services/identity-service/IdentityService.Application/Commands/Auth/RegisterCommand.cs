using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MassTransit;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Auth;

public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResult>;

public sealed class RegisterCommandHandler(IUserRepository users, IPublishEndpoint publisher, IOptions<JwtOptions> jwt, ILogger<RegisterCommandHandler> logger) : IRequestHandler<RegisterCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return new() { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "All fields required" };

        if (request.Password.Length < 8)
            return new() { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Password must be at least 8 characters" };

        var email = request.Email.Trim().ToLowerInvariant();
        var existingUser = await users.GetByEmailAsync(email, ct);
        if (existingUser != null)
        {
            logger.LogWarning("Registration failed: email {Email} already exists", email);
            return new() { Success = false, ErrorCode = ErrorCodes.DuplicateEmail, Message = "Email exists" };
        }

        var otp = IdentityHelpers.GenerateOtpCode();
        var now = DateTime.UtcNow;

        var user = new IdentityUser
        {
            Id = Guid.NewGuid(), Email = email, FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false, EmailVerificationOtp = otp,
            EmailVerificationOtpExpiresAtUtc = now.AddMinutes(10),
            Status = UserStatus.PendingVerification, Role = UserRole.User,
            CreatedAtUtc = now, UpdatedAtUtc = now
        };

        try
        {
            await users.AddAsync(user, ct);
            logger.LogInformation("User created: {UserId}, {Email}", user.Id, email);

            await publisher.Publish(new { user.Id, user.Email, user.FullName, OtpCode = otp, Purpose = "EmailVerification", ExpiresAtUtc = user.EmailVerificationOtpExpiresAtUtc }, ct);
            logger.LogInformation("Published IUserOtpGenerated for {UserId}", user.Id);

            await publisher.Publish(new { UserId = user.Id, Email = user.Email, FullName = user.FullName, CreatedAtUtc = user.CreatedAtUtc }, ct);
            logger.LogInformation("Published IUserRegistered for {UserId}", user.Id);

            return new()
            {
                Success = true, Message = "Registered",
                AccessToken = IdentityHelpers.GenerateAccessToken(user, jwt.Value),
                User = IdentityHelpers.ToUserSummary(user)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create user {Email}: {Error}", email, ex.Message);
            return new() { Success = false, ErrorCode = "InternalError", Message = "Registration failed. Please try again." };
        }
    }
}
