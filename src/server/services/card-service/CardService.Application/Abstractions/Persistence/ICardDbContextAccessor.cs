namespace CardService.Application.Abstractions.Persistence;

public interface ICardDbContextAccessor
{
    Task<bool> AnyAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class;
    Task<T?> FindAsync<T>(params object[] keyValues) where T : class;
    void Add<T>(T entity) where T : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
