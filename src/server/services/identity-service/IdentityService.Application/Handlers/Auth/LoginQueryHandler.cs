using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Application.Queries.Auth;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Handlers.Auth;

public sealed class LoginQueryHandler(IUserRepository userRepository, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<LoginQuery, AuthResult>
{
    public async Task<AuthResult> Handle(LoginQuery request, CancellationToken cancellationToken)
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

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidCredentials,
                Message = "Invalid email or password."
            };
        }

        if (user.Status is not UserStatus.Active and not UserStatus.PendingVerification)
        {
            return new AuthResult
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = $"User status is {IdentityHelpers.ToApiStatus(user.Status)}. Login not allowed."
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
