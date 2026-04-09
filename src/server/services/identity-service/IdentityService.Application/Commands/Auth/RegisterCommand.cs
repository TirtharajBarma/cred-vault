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

/// <summary>
/// Command to register a new user account.
/// Validates input, checks for duplicate email, creates user with OTP for email verification.
/// Publishes events for OTP generation and user registration for other services to consume.
/// </summary>
/// <param name="Email">User's email address (will be normalized to lowercase)</param>
/// <param name="Password">User's password (must be at least 8 characters)</param>
/// <param name="FullName">User's full name</param>
public record RegisterCommand(string Email, string Password, string FullName) : IRequest<AuthResult>;

/// <summary>
/// Handler for RegisterCommand.
/// 1. Validates all fields are provided and password meets minimum length
/// 2. Checks if email already exists in the database
/// 3. Generates OTP code for email verification (valid for 10 minutes)
/// 4. Creates new user with PendingVerification status
/// 5. Publishes IUserOtpGenerated event (NotificationService consumes this to send email)
/// 6. Publishes IUserRegistered event (other services can react to new user creation)
/// 7. Returns AuthResult with access token for immediate login
/// </summary>
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

            await publisher.Publish<IUserOtpGenerated>(new { UserId = user.Id, user.Email, user.FullName, OtpCode = otp, Purpose = "EmailVerification", ExpiresAtUtc = user.EmailVerificationOtpExpiresAtUtc }, ct);
            logger.LogInformation("Published IUserOtpGenerated for {UserId}", user.Id);

            await publisher.Publish<IUserRegistered>(new { UserId = user.Id, Email = user.Email, FullName = user.FullName, CreatedAtUtc = user.CreatedAtUtc }, ct);
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
