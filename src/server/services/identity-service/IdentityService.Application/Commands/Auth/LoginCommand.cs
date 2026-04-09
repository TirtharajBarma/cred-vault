using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Commands.Auth;

/// <summary>
/// Command to authenticate user with email and password credentials.
/// Validates credentials against database and returns JWT access token on success.
/// Implements timing attack prevention by always performing password hash verification.
/// </summary>
/// <param name="Email">User's registered email address</param>
/// <param name="Password">User's password</param>
public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

/// <summary>
/// Handler for LoginCommand:
/// 1. Validates email and password are provided
/// 2. Normalizes email to lowercase for lookup
/// 3. Fetches user by email from repository
/// 4. If user not found: uses dummy hash to prevent timing attacks (same response time as valid user)
/// 5. If found but no password: returns invalid credentials (handles Google SSO users without password)
/// 6. Verifies password using BCrypt against stored hash
/// 7. Checks user status is Active (not blocked/suspended/pending verification)
/// 8. Generates JWT access token with user claims (sub: userId, role, email)
/// 9. Returns AuthResult with token and user summary
/// </summary>
public sealed class LoginCommandHandler(IUserRepository userRepository, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<LoginCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Email and Password are required."
            };
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user is null)
        {
            BCrypt.Net.BCrypt.Verify(request.Password, "$2a$12$000000000000000000000u.Yl7H7vqzNdKdH8R3O.RKQ3fJx5y"); // Dummy hash to prevent timing attack
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidCredentials,
                Message = "Invalid email or password."
            };
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidCredentials,
                Message = "Invalid email or password."
            };
        }

        if (user.Status != UserStatus.Active)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.AccountLocked,
                Message = "User is not active. Please verify your email first."
            };
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
