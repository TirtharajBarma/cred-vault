using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Models;

namespace BillingService.Application.Queries.Rewards;

public record GetRewardTransactionsQuery(Guid UserId) : IRequest<ApiResponse<List<RewardTransaction>>>;

public class GetRewardTransactionsQueryHandler(IRewardRepository rewardRepository)
    : IRequestHandler<GetRewardTransactionsQuery, ApiResponse<List<RewardTransaction>>>
{
    public async Task<ApiResponse<List<RewardTransaction>>> Handle(GetRewardTransactionsQuery request, CancellationToken cancellationToken)
    {
        var account = await rewardRepository.GetAccountByUserIdAsync(request.UserId, cancellationToken);
        if (account is null)
        {
            return new ApiResponse<List<RewardTransaction>> { Success = true, Message = "No account found.", Data = [] };
        }

        var transactions = await rewardRepository.GetTransactionsByAccountIdAsync(account.Id, cancellationToken);
        return new ApiResponse<List<RewardTransaction>> { Success = true, Message = "Transactions fetched.", Data = transactions };
    }
}