using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Application.Queries.Users;
using MediatR;

namespace IdentityService.Application.Handlers.Users;

public sealed class GetMeQueryHandler(IUserRepository userRepository) : IRequestHandler<GetMeQuery, UserResult>
{
    public async Task<UserResult> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user is null
            ? new UserResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." }
            : new UserResult { Success = true, Message = "User profile fetched.", User = IdentityHelpers.ToUserSummary(user) };
    }
}
