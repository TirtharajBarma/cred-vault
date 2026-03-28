using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Enums;

namespace BillingService.Infrastructure.Persistence.Sql.Repositories;

public sealed class SqlRewardRepository(BillingDbContext dbContext) : IRewardRepository
{
    public async Task<List<RewardTier>> GetTiersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTiers
            .AsNoTracking()
            .OrderBy(x => x.CardNetwork)
            .ThenByDescending(x => x.MinSpend)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<RewardTier?> GetBestMatchingTierAsync(CardNetwork network, Guid? issuerId, decimal amount, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTiers
            .AsNoTracking()
            .Where(x => x.CardNetwork == network && 
                        (x.IssuerId == issuerId || x.IssuerId == null) &&
                        x.EffectiveFromUtc <= dateUtc && 
                        (x.EffectiveToUtc == null || x.EffectiveToUtc >= dateUtc) &&
                        amount >= x.MinSpend)
            .OrderByDescending(x => x.IssuerId != null) // Prioritize Issuer-specific tiers over network defaults
            .ThenByDescending(x => x.MinSpend)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddTierAsync(RewardTier tier, CancellationToken cancellationToken = default)
    {
        await dbContext.RewardTiers.AddAsync(tier, cancellationToken);
    }

    public async Task<RewardAccount?> GetAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task AddAccountAsync(RewardAccount account, CancellationToken cancellationToken = default)
    {
        await dbContext.RewardAccounts.AddAsync(account, cancellationToken);
    }

    public Task UpdateAccountAsync(RewardAccount account, CancellationToken cancellationToken = default)
    {
        dbContext.RewardAccounts.Update(account);
        return Task.CompletedTask;
    }

    public async Task<List<RewardTransaction>> GetTransactionsByAccountIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTransactions
            .AsNoTracking()
            .Where(x => x.RewardAccountId == accountId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTransactionAsync(RewardTransaction transaction, CancellationToken cancellationToken = default)
    {
        await dbContext.RewardTransactions.AddAsync(transaction, cancellationToken);
    }
}
