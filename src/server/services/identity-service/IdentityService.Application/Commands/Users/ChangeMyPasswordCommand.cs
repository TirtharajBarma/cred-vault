using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Commands.Users;

public sealed record ChangeMyPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<OperationResult>;

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
