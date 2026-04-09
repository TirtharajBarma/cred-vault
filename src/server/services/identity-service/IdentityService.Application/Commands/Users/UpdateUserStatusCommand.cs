using IdentityService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs;
using IdentityService.Domain.Enums;
using MediatR;
using Shared.Contracts.Exceptions;

namespace IdentityService.Application.Commands.Users;

public sealed record UpdateUserStatusCommand(Guid UserId, UserStatus Status) : IRequest<OperationResult>;

public sealed class UpdateUserStatusCommandHandler(IUserRepository userRepository)
    : IRequestHandler<UpdateUserStatusCommand, OperationResult>
{
    public async Task<OperationResult> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) throw new NotFoundException("User", request.UserId);

        if (request.Status == UserStatus.Active && !user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            user.EmailVerificationOtp = null;
            user.EmailVerificationOtpExpiresAtUtc = null;
        }
        else if (request.Status == UserStatus.PendingVerification)
        {
            user.IsEmailVerified = false;
        }

        user.Status = request.Status;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        return new OperationResult { Success = true, Message = "Status updated." };
    }
}
