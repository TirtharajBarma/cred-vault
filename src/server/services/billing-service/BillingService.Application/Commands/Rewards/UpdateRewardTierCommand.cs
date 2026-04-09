using MediatR;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BillingService.Application.Commands.Rewards;

public sealed record UpdateRewardTierCommand(
    Guid Id,
    string CardNetwork,
    Guid? IssuerId,
    decimal MinSpend,
    decimal RewardRate,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc
) : IRequest<ApiResponse<RewardTier>>;

public sealed class UpdateRewardTierCommandHandler(
    IRewardRepository rewardRepository,
    IUnitOfWork unitOfWork
) : IRequestHandler<UpdateRewardTierCommand, ApiResponse<RewardTier>>
{
    public async Task<ApiResponse<RewardTier>> Handle(UpdateRewardTierCommand request, CancellationToken cancellationToken)
    {
        var tier = await rewardRepository.GetTierByIdAsync(request.Id, cancellationToken);
        if (tier is null)
        {
            return new ApiResponse<RewardTier> { Success = false, Message = "Reward tier not found." };
        }

        if (!Enum.TryParse<CardNetwork>(request.CardNetwork, true, out var network))
        {
            return new ApiResponse<RewardTier> { Success = false, Message = "Invalid CardNetwork." };
        }

        tier.CardNetwork = network;
        tier.IssuerId = request.IssuerId;
        tier.MinSpend = request.MinSpend;
        tier.RewardRate = request.RewardRate;
        tier.EffectiveFromUtc = request.EffectiveFromUtc;
        tier.EffectiveToUtc = request.EffectiveToUtc;
        tier.UpdatedAtUtc = DateTime.UtcNow;

        await rewardRepository.UpdateTierAsync(tier, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<RewardTier> { Success = true, Message = "Reward tier updated.", Data = tier };
    }
}
