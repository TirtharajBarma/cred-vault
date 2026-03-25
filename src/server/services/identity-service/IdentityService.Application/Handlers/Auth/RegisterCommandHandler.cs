using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.Configuration;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Application.Commands.Auth;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Handlers.Auth;

public sealed class RegisterCommandHandler(IUserRepository userRepository, IOptions<JwtOptions> jwtOptions)
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

        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsEmailVerified = false,
            Status = UserStatus.PendingVerification,
            Role = UserRole.User,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await userRepository.AddAsync(user, cancellationToken);

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
