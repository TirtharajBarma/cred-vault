using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence.Sql;

public sealed class SqlCardRepository(CardDbContext dbContext) : ICardRepository
{
    public async Task AddAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        await dbContext.CreditCards.AddAsync(card, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CreditCard?> GetByIdAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return dbContext.CreditCards
            .Include(x => x.Issuer)
            .FirstOrDefaultAsync(x => x.Id == cardId, cancellationToken);
    }

    public Task<CreditCard?> GetByUserAndIdAsync(Guid userId, Guid cardId, CancellationToken cancellationToken = default)
    {
        return dbContext.CreditCards
            .Include(x => x.Issuer)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == cardId, cancellationToken);
    }

    public Task<List<CreditCard>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.CreditCards
            .Include(x => x.Issuer)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        dbContext.CreditCards.Update(card);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CreditCard card, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        card.IsDeleted = true;
        card.DeletedAtUtc = now;
        card.IsDefault = false;
        card.UpdatedAtUtc = now;

        dbContext.CreditCards.Update(card);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnsetDefaultForUserAsync(Guid userId, Guid? exceptCardId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.CreditCards.Where(x => x.UserId == userId && x.IsDefault);
        if (exceptCardId.HasValue)
        {
            query = query.Where(x => x.Id != exceptCardId.Value);
        }

        var cards = await query.ToListAsync(cancellationToken);
        if (cards.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var card in cards)
        {
            card.IsDefault = false;
            card.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<CardIssuer?> GetIssuerByNetworkAsync(CardNetwork network, CancellationToken cancellationToken = default)
    {
        return dbContext.CardIssuers.FirstOrDefaultAsync(x => x.IsActive && x.Network == network, cancellationToken);
    }

    public Task<CardIssuer?> GetIssuerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.CardIssuers.FirstOrDefaultAsync(x => x.IsActive && x.Id == id, cancellationToken);
    }

    public Task<List<CardIssuer>> ListIssuersAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.CardIssuers
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasDuplicateCardAsync(Guid userId, CardNetwork network, string last4, CancellationToken cancellationToken = default)
    {
        return dbContext.CreditCards
            .Include(x => x.Issuer)
            .AnyAsync(x => x.UserId == userId 
                        && x.Issuer!.Network == network 
                        && x.Last4 == last4
                        && !x.IsDeleted, cancellationToken);
    }

    public async Task AddTransactionAsync(CardTransaction transaction, CancellationToken cancellationToken = default)
    {
        await dbContext.CardTransactions.AddAsync(transaction, cancellationToken);
        // Note: SaveChangesAsync left to caller if part of larger unit of work, but we'll call it here if stand-alone
        // The original controller relied on UpdateAsync(card) to commit the transaction added to _db context.
        // To keep it simple, we'll let UpdateAsync(card) flush the changes if used together, 
        // but for safety, we DO NOT call SaveChangesAsync here to allow atomic transactions with UpdateAsync.
    }

    public Task<List<CardTransaction>> GetTransactionsByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.CardTransactions
            .AsNoTracking()
            .Where(x => x.CardId == cardId && x.UserId == userId)
            .OrderByDescending(x => x.DateUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<CardTransaction>> GetTransactionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.CardTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.DateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDuplicateTransactionAsync(Guid cardId, TransactionType type, decimal amount, string description, DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        var tolerance = TimeSpan.FromMinutes(1);
        var minDate = dateUtc.Add(-tolerance);
        var maxDate = dateUtc.Add(tolerance);

        return await dbContext.CardTransactions
            .AnyAsync(x => x.CardId == cardId 
                && x.Type == type 
                && x.Amount == amount
                && x.Description == description
                && x.DateUtc >= minDate 
                && x.DateUtc <= maxDate,
                cancellationToken);
    }
}
