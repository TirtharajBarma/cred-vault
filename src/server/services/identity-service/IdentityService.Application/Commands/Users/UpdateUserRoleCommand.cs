using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Enums;
using MediatR;
using Shared.Contracts.DTOs;
using Shared.Contracts.Exceptions;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateUserRoleCommand(Guid UserId, UserRole Role) : IRequest<OperationResult>;

public sealed class UpdateUserRoleCommandHandler(IUserRepository userRepository) 
    : IRequestHandler<UpdateUserRoleCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        user.Role = request.Role;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = $"User role updated to {user.Role}" };
    }
}
