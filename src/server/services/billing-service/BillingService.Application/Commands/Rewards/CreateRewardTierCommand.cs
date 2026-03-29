using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;
using Shared.Contracts.Enums;

namespace BillingService.Application.Commands.Rewards;

public record CreateRewardTierCommand(
    string CardNetwork,
    Guid? IssuerId,
    decimal MinSpend,
    decimal RewardRate,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc) : IRequest<ApiResponse<RewardTier>>;

public class CreateRewardTierCommandHandler(IRewardRepository rewardRepository, IUnitOfWork unitOfWork) 
    : IRequestHandler<CreateRewardTierCommand, ApiResponse<RewardTier>>
{
    public async Task<ApiResponse<RewardTier>> Handle(CreateRewardTierCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CardNetwork>(request.CardNetwork, true, out var network))
            return new ApiResponse<RewardTier> { Success = false, Message = "Invalid CardNetwork." };

        var tier = new RewardTier
        {
            Id = Guid.NewGuid(),
            CardNetwork = network,
            IssuerId = request.IssuerId,
            MinSpend = request.MinSpend,
            RewardRate = request.RewardRate,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await rewardRepository.AddTierAsync(tier, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApiResponse<RewardTier> { Success = true, Message = "Reward tier created.", Data = tier };
    }
}
