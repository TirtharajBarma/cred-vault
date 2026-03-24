using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Users;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Handlers.Users;

public sealed class UpdateUserStatusCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateUserStatusCommand, UserResult>
{
    public async Task<UserResult> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (!IdentityHelpers.TryParseStatus(request.Status, out var status))
        {
            return new UserResult
            {
                Success = false,
                ErrorCode = ErrorCodes.InvalidStatus,
                Message = "Invalid status. Use active, suspended, blocked, pending-verification."
            };
        }

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        user.Status = status;
        if (user.Status == UserStatus.Active)
        {
            user.IsEmailVerified = true;
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new UserResult { Success = true, Message = "User status updated.", User = IdentityHelpers.ToUserSummary(user) };
    }
}
