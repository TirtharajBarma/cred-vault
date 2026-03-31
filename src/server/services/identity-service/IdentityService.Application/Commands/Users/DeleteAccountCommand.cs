using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Contracts.DTOs;
using Shared.Contracts.Events.Identity;

namespace IdentityService.Application.Commands.Users;

public sealed record DeleteAccountCommand(Guid UserId) : IRequest<OperationResult>;

public sealed class DeleteAccountCommandHandler(
    IUserRepository userRepository,
    IPublishEndpoint publisher,
    ILogger<DeleteAccountCommandHandler> logger)
    : IRequestHandler<DeleteAccountCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            logger.LogWarning("DeleteAccount failed: user {UserId} not found", request.UserId);
            return new OperationResult { Success = false, Message = "User not found." };
        }

        if (user.Status == UserStatus.Deleted)
        {
            logger.LogWarning("DeleteAccount failed: user {UserId} already deleted", request.UserId);
            return new OperationResult { Success = false, Message = "Account already deleted." };
        }

        await userRepository.SoftDeleteAsync(request.UserId, cancellationToken);
        logger.LogInformation("User {UserId} soft-deleted, status set to Deleted", request.UserId);

        try
        {
            await publisher.Publish(new { UserId = user.Id, DeletedAtUtc = DateTime.UtcNow }, cancellationToken);
            logger.LogInformation("Published IUserDeleted for {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish IUserDeleted for {UserId}", user.Id);
        }

        return new OperationResult { Success = true, Message = "Account deleted successfully." };
    }
}
