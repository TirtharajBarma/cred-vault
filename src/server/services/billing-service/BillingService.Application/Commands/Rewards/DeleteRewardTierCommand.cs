using MediatR;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;

namespace BillingService.Application.Commands.Rewards;

public sealed record DeleteRewardTierCommand(Guid Id) : IRequest<DeleteRewardTierResult>;

public sealed class DeleteRewardTierCommandHandler(IRewardRepository rewardRepository) : IRequestHandler<DeleteRewardTierCommand, DeleteRewardTierResult>
{
    public async Task<DeleteRewardTierResult> Handle(DeleteRewardTierCommand request, CancellationToken cancellationToken)
    {
        var tier = await rewardRepository.GetTierByIdAsync(request.Id, cancellationToken);
        
        if (tier == null)
        {
            return new DeleteRewardTierResult
            {
                Success = false,
                Message = "Reward tier not found."
            };
        }

        if (await rewardRepository.HasAccountsByTierAsync(request.Id, cancellationToken))
        {
            return new DeleteRewardTierResult
            {
                Success = false,
                Message = "Cannot delete tier with existing reward accounts. Reassign or remove accounts first."
            };
        }

        await rewardRepository.DeleteTierAsync(tier, cancellationToken);

        return new DeleteRewardTierResult
        {
            Success = true,
            Message = "Reward tier deleted successfully."
        };
    }
}

public sealed class DeleteRewardTierResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}