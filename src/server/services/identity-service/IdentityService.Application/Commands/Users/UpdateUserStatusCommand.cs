using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.DTOs.Responses;
using IdentityService.Domain.Enums;
using MediatR;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateUserStatusCommand(Guid UserId, UserStatus Status) : IRequest<OperationResult>;

public sealed class UpdateUserStatusCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateUserStatusCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return new OperationResult { Success = false, Message = "User not found." };

        user.Status = request.Status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = "Status updated." };
    }
}
