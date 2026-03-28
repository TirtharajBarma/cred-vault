using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Infrastructure.Persistence.Sql.Repositories;

public sealed class SqlBillRepository(BillingDbContext dbContext) : IBillRepository
{
    public async Task<Bill?> GetByIdAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Bills.FirstOrDefaultAsync(x => x.Id == billId, cancellationToken);
    }

    public async Task<Bill?> GetByIdAndUserIdAsync(Guid billId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Bills.FirstOrDefaultAsync(x => x.Id == billId && x.UserId == userId, cancellationToken);
    }

    public async Task<List<Bill>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Bills
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.BillingDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        await dbContext.Bills.AddAsync(bill, cancellationToken);
    }

    public Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        dbContext.Bills.Update(bill);
        return Task.CompletedTask;
    }
}
