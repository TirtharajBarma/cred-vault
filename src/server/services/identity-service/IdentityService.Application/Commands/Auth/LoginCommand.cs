using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using Shared.Contracts.DTOs.Identity.Responses;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Commands.Auth;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResult>;

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

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
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
