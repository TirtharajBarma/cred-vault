using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs;
using Shared.Contracts.DTOs.Identity.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Users;

/// <summary>
/// Command for authenticated user to change their own password.
/// Requires current password for verification before setting new password.
/// </summary>
/// <param name="UserId">Current user's ID (from JWT token)</param>
/// <param name="CurrentPassword">User's current password for verification</param>
/// <param name="NewPassword">New password to set (min 8 characters)</param>
public sealed record ChangeMyPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<OperationResult>;

/// <summary>
/// Handler for ChangeMyPasswordCommand:
/// 1. Fetches user by ID
/// 2. Verifies current password matches stored hash (using BCrypt)
/// 3. If invalid, returns error
/// 4. On success: hashes new password with BCrypt, updates user record
/// </summary>
public sealed class ChangeMyPasswordCommandHandler(IUserRepository userRepository)
    : IRequestHandler<ChangeMyPasswordCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.InvalidCredentials, Message = "Invalid current password." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = "Password changed successfully." };
    }
}
