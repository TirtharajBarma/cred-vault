using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;

namespace BillingService.Application.Queries.Rewards;

public record GetMyRewardAccountQuery(Guid UserId) : IRequest<ApiResponse<RewardAccount?>>;
public record ListMyRewardTransactionsQuery(Guid UserId) : IRequest<ApiResponse<List<RewardTransaction>>>;

public class RewardAccountQueryHandler(IRewardRepository rewardRepository)
    : IRequestHandler<GetMyRewardAccountQuery, ApiResponse<RewardAccount?>>,
      IRequestHandler<ListMyRewardTransactionsQuery, ApiResponse<List<RewardTransaction>>>
{
    public async Task<ApiResponse<RewardAccount?>> Handle(GetMyRewardAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        return new ApiResponse<RewardAccount?> { Success = true, Message = "Account fetched.", Data = account };
    }

    public async Task<ApiResponse<List<RewardTransaction>>> Handle(ListMyRewardTransactionsQuery request, CancellationToken cancellationToken)
    {
        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        if (account is null) return new ApiResponse<List<RewardTransaction>> { Success = true, Data = [] };

        var txs = await rewardRepository.GetTransactionsByAccountIdAsync(account.Id, cancellationToken);
        return new ApiResponse<List<RewardTransaction>> { Success = true, Message = "Transactions fetched.", Data = txs };
    }
}
