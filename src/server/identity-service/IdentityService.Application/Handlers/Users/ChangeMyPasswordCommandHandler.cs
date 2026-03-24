using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Users;
using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Handlers.Users;

public sealed class ChangeMyPasswordCommandHandler(IUserRepository userRepository)
    : IRequestHandler<ChangeMyPasswordCommand, OperationResult>
{
    public async Task<OperationResult> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.InvalidCredentials, Message = "Current password is incorrect." };
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return new OperationResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "New password must be at least 8 characters." };
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = "Password changed successfully." };
    }
}
