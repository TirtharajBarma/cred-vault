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
                        (x.EffectiveToUtc == null || x.EffectiveToUtc > dateUtc) &&
                        amount >= x.MinSpend)
            .OrderByDescending(x => x.IssuerId != null) // Prioritize Issuer-specific tiers over network defaults
            .ThenByDescending(x => x.MinSpend)          // Higher spend threshold first
            .ThenByDescending(x => x.RewardRate)        // Higher reward rate for same MinSpend
            .FirstOrDefaultAsync(cancellationToken);
    }
    // match card network
    // match issuer
    // is active for that date
    // prefer issuer-specific
    // prefer higher spend tier
    // prefer higher reward rate

    public async Task<RewardTier?> GetTierByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTiers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task DeleteTierAsync(RewardTier tier, CancellationToken cancellationToken = default)
    {
        dbContext.RewardTiers.Remove(tier);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddTierAsync(RewardTier tier, CancellationToken cancellationToken = default)
    {
        await dbContext.RewardTiers.AddAsync(tier, cancellationToken);
    }

    public Task UpdateTierAsync(RewardTier tier, CancellationToken cancellationToken = default)
    {
        dbContext.RewardTiers.Update(tier);
        return Task.CompletedTask;
    }

    public async Task<RewardAccount?> GetAccountByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    }

    public async Task<List<RewardAccount>> GetAccountsByTierIdAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardAccounts
            .Where(x => x.RewardTierId == tierId)
            .ToListAsync(cancellationToken);
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

    public async Task<bool> HasTransactionForBillAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTransactions
            .AnyAsync(x => x.BillId == billId && x.Type == RewardTransactionType.Earned, cancellationToken);
    }

    public async Task<RewardTransaction?> GetTransactionByBillIdAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        return await dbContext.RewardTransactions
            .FirstOrDefaultAsync(x => x.BillId == billId && x.Type == RewardTransactionType.Earned, cancellationToken);
    }

    public Task UpdateTransactionAsync(RewardTransaction transaction, CancellationToken cancellationToken = default)
    {
        dbContext.RewardTransactions.Update(transaction);
        return Task.CompletedTask;
    }

    public Task<bool> HasAccountsByTierAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        return dbContext.RewardAccounts.AnyAsync(x => x.RewardTierId == tierId, cancellationToken);
    }
}
