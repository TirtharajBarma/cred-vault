using BillingService.Domain.Entities;
using Shared.Contracts.Enums;

namespace BillingService.Application.Abstractions.Persistence;

public interface IRewardRepository
{
    Task<List<RewardTier>> GetTiersAsync(CancellationToken cancellationToken = default);
    Task<RewardTier?> GetTierByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RewardTier?> GetBestMatchingTierAsync(CardNetwork network, Guid? issuerId, decimal amount, DateTime dateUtc, CancellationToken cancellationToken = default);
    Task AddTierAsync(RewardTier tier, CancellationToken cancellationToken = default);
    Task UpdateTierAsync(RewardTier tier, CancellationToken cancellationToken = default);
    Task DeleteTierAsync(RewardTier tier, CancellationToken cancellationToken = default);

    Task<RewardAccount?> GetAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAccountAsync(RewardAccount account, CancellationToken cancellationToken = default);
    Task UpdateAccountAsync(RewardAccount account, CancellationToken cancellationToken = default);

    Task<List<RewardTransaction>> GetTransactionsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(RewardTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionForBillAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<RewardTransaction?> GetTransactionByBillIdAsync(Guid billId, CancellationToken cancellationToken = default);
    Task UpdateTransactionAsync(RewardTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> HasAccountsByTierAsync(Guid tierId, CancellationToken cancellationToken = default);
}
