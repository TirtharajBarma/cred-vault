using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using IdentityService.Application.DTOs.Responses;
using MediatR;

namespace IdentityService.Application.Queries.Users;

public sealed record GetUserByIdQuery(Guid UserId) : IRequest<AuthResult>;

public sealed class GetUserByIdQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetUserByIdQuery, AuthResult>
{
    public async Task<AuthResult> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new AuthResult { Success = false, ErrorCode = ErrorCodes.UserNotFound, Message = "User not found." };

        return new AuthResult { Success = true, User = IdentityHelpers.ToUserSummary(user) };
    }
}
