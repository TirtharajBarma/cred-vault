using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;

namespace BillingService.Application.Queries.Rewards;

public record ListRewardTiersQuery() : IRequest<ApiResponse<List<RewardTier>>>;

public class ListRewardTiersQueryHandler(IRewardRepository rewardRepository)
    : IRequestHandler<ListRewardTiersQuery, ApiResponse<List<RewardTier>>>
{
    public async Task<ApiResponse<List<RewardTier>>> Handle(ListRewardTiersQuery request, CancellationToken cancellationToken)
    {
        var tiers = await rewardRepository.GetTiersAsync(cancellationToken);
        return new ApiResponse<List<RewardTier>> { Success = true, Message = "Tiers fetched.", Data = tiers };
    }
}
