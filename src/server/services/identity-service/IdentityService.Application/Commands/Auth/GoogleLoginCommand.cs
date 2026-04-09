using Google.Apis.Auth;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;

namespace IdentityService.Application.Commands.Auth;

/// <summary>
/// Command to authenticate or register user via Google OAuth (Single Sign-On).
/// Validates Google ID token, auto-registers new users if not exists, returns JWT on success.
/// </summary>
/// <param name="IdToken">Google OAuth ID token from Google Sign-In</param>
public sealed record GoogleLoginCommand(string IdToken) : IRequest<AuthResult>;

/// <summary>
/// Handler for GoogleLoginCommand:
/// 1. Validates IdToken is provided
/// 2. Validates Google token using Google Json Web Signature library with configured ClientId
/// 3. Extracts email from Google's payload (email already verified by Google)
/// 4. Looks up user by email - if not found, auto-creates with Active status (Google verified email)
/// 5. If user found but blocked, returns error
/// 6. Generates JWT access token and returns user summary
/// </summary>
public sealed class GoogleLoginCommandHandler(
    IUserRepository userRepository,
    IOptions<JwtOptions> jwtOptions,
    IConfiguration configuration,
    ILogger<GoogleLoginCommandHandler> logger)
    : IRequestHandler<GoogleLoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(GoogleLoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return new AuthResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Google credential is required." };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var clientId = configuration["Google:ClientId"]
                ?? throw new InvalidOperationException("Google:ClientId is not configured.");

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            };

            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Invalid Google id_token received");
            return new AuthResult { Success = false, ErrorCode = ErrorCodes.InvalidCredentials, Message = "Invalid Google credential. Please try again." };
        }

        var email = payload.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return new AuthResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "Could not retrieve email from Google account." };

        var user = await userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            // Auto-register — Google already verified the email
            var now = DateTime.UtcNow;
            user = new IdentityUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = payload.Name ?? email,
                PasswordHash = null,          // Google users have no password
                IsEmailVerified = true,
                Status = UserStatus.Active,
                Role = UserRole.User,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            await userRepository.AddAsync(user, cancellationToken);
            logger.LogInformation("Auto-registered Google user: {UserId}, {Email}", user.Id, email);
        }
        else if (user.Status == UserStatus.Blocked)
        {
            return new AuthResult { Success = false, ErrorCode = ErrorCodes.AccountLocked, Message = "Your account has been blocked." };
        }

        var accessToken = IdentityHelpers.GenerateAccessToken(user, jwtOptions.Value);

        return new AuthResult
        {
            Success = true,
            Message = "Login successful.",
            AccessToken = accessToken,
            User = IdentityHelpers.ToUserSummary(user)
        };
    }
}
