using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Users;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Handlers.Users;

public sealed class UpdateMeCommandHandler(IUserRepository userRepository) : IRequestHandler<UpdateMeCommand, UserResult>
{
    public async Task<UserResult> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return new UserResult { Success = false, ErrorCode = ErrorCodes.ValidationError, Message = "FullName is required." };
        }

        user.FullName = request.FullName.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new UserResult { Success = true, Message = "Profile updated.", User = IdentityHelpers.ToUserSummary(user) };
    }
}
