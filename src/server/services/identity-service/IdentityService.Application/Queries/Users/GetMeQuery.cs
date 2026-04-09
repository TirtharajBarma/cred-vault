using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs.Identity.Responses;
using MediatR;
using Shared.Contracts.Exceptions;

namespace IdentityService.Application.Queries.Users;

public sealed record GetMeQuery(Guid UserId) : IRequest<AuthResult>;

public sealed class GetMeQueryHandler(IUserRepository userRepository)
    : IRequestHandler<GetMeQuery, AuthResult>
{
    public async Task<AuthResult> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("User", request.UserId);

        return new AuthResult { Success = true, User = IdentityHelpers.ToUserSummary(user) };
    }
}
