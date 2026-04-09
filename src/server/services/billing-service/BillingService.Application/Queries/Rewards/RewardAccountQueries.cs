using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;

namespace BillingService.Application.Queries.Rewards;

public record GetMyRewardAccountQuery(Guid UserId) : IRequest<ApiResponse<RewardAccount?>>;

public class RewardAccountQueryHandler(IRewardRepository rewardRepository)
    : IRequestHandler<GetMyRewardAccountQuery, ApiResponse<RewardAccount?>>
{
    public async Task<ApiResponse<RewardAccount?>> Handle(GetMyRewardAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        return new ApiResponse<RewardAccount?> { Success = true, Message = "Account fetched.", Data = account };
    }
}
