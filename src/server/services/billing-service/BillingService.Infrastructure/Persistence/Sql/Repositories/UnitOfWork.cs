using BillingService.Application.Abstractions.Persistence;
using BillingService.Infrastructure.Persistence.Sql;

namespace BillingService.Infrastructure.Persistence.Sql.Repositories;

public sealed class UnitOfWork(BillingDbContext dbContext) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
