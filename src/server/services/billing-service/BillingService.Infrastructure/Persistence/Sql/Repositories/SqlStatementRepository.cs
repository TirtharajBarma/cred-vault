using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Infrastructure.Persistence.Sql.Repositories;

public sealed class SqlStatementRepository(BillingDbContext dbContext) : IStatementRepository
{
    public async Task<Statement?> GetByIdAsync(Guid statementId, CancellationToken ct = default)
    {
        return await dbContext.Statements.FirstOrDefaultAsync(x => x.Id == statementId, ct);
    }

    public async Task<List<Statement>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await dbContext.Statements
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.PeriodEndUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Statement>> GetByCardIdAsync(Guid cardId, CancellationToken ct = default)
    {
        return await dbContext.Statements
            .AsNoTracking()
            .Where(x => x.CardId == cardId)
            .OrderByDescending(x => x.PeriodEndUtc)
            .ToListAsync(ct);
    }

    public async Task<List<Statement>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.Statements
            .AsNoTracking()
            .OrderByDescending(x => x.PeriodEndUtc)
            .ToListAsync(ct);
    }

    public async Task<List<StatementTransaction>> GetTransactionsAsync(Guid statementId, CancellationToken ct = default)
    {
        return await dbContext.StatementTransactions
            .AsNoTracking()
            .Where(x => x.StatementId == statementId)
            .OrderBy(x => x.DateUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsForCardInPeriodAsync(Guid cardId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
    {
        return await dbContext.Statements
            .AnyAsync(x => x.CardId == cardId && x.PeriodStartUtc == periodStart && x.PeriodEndUtc == periodEnd, ct);
    }

    public async Task AddAsync(Statement statement, CancellationToken ct = default)
    {
        await dbContext.Statements.AddAsync(statement, ct);
    }

    public async Task AddTransactionsAsync(List<StatementTransaction> transactions, CancellationToken ct = default)
    {
        await dbContext.StatementTransactions.AddRangeAsync(transactions, ct);
    }

    public Task UpdateAsync(Statement statement, CancellationToken ct = default)
    {
        dbContext.Statements.Update(statement);
        return Task.CompletedTask;
    }
}
