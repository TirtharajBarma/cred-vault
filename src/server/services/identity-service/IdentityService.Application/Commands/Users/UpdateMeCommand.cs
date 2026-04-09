using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Common;
using Shared.Contracts.DTOs.Identity.Responses;
using MediatR;
using Shared.Contracts.Exceptions;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateMeCommand(Guid UserId, string FullName) : IRequest<AuthResult>;

public sealed class UpdateMeCommandHandler(IUserRepository userRepository, Microsoft.Extensions.Options.IOptions<Shared.Contracts.Configuration.JwtOptions> jwtOptions)
    : IRequestHandler<UpdateMeCommand, AuthResult>
{
    public async Task<AuthResult> Handle(UpdateMeCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("User", request.UserId);

        user.FullName = request.FullName.Trim();
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        var accessToken = IdentityHelpers.GenerateAccessToken(user, jwtOptions.Value);
        return new AuthResult { Success = true, Message = "Profile updated.", AccessToken = accessToken, User = IdentityHelpers.ToUserSummary(user) };
    }
}
