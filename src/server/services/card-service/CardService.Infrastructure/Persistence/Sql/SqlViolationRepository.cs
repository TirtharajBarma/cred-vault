using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence.Sql;

public sealed class SqlViolationRepository(CardDbContext dbContext) : IViolationRepository
{
    public async Task<CardViolation?> GetByIdAsync(Guid violationId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CardViolations
            .Include(x => x.Card)
            .FirstOrDefaultAsync(x => x.Id == violationId, cancellationToken);
    }

    public async Task<CardViolation?> GetActiveViolationByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CardViolations
            .Where(x => x.CardId == cardId && x.IsActive)
            .OrderByDescending(x => x.AppliedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<CardViolation>> GetViolationsByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CardViolations
            .AsNoTracking()
            .Where(x => x.CardId == cardId)
            .OrderByDescending(x => x.AppliedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CardViolation violation, CancellationToken cancellationToken = default)
    {
        await dbContext.CardViolations.AddAsync(violation, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CardViolation violation, CancellationToken cancellationToken = default)
    {
        dbContext.CardViolations.Update(violation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
