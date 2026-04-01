using System.Linq.Expressions;
using CardService.Application.Abstractions.Persistence;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

namespace CardService.Infrastructure.Persistence;

public class CardDbContextAccessor : ICardDbContextAccessor
{
    private readonly CardDbContext _context;

    public CardDbContextAccessor(CardDbContext context)
    {
        _context = context;
    }

    public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class
    {
        return await _context.Set<T>().AnyAsync(predicate, cancellationToken);
    }

    public Task<T?> FindAsync<T>(params object[] keyValues) where T : class
    {
        return Task.FromResult(_context.Set<T>().Find(keyValues));
    }

    public void Add<T>(T entity) where T : class
    {
        _context.Set<T>().Add(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
