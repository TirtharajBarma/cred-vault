using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Sql;

public sealed class SqlUserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<IdentityUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public Task<IdentityUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public async Task UpdateAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<IdentityUser> Users, int TotalCount)> ListAllAsync(int page, int pageSize, string? search, UserStatus? status, CancellationToken ct = default)
    {
        var query = dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(x => x.FullName.ToLower().Contains(searchLower) || x.Email.ToLower().Contains(searchLower));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (users, totalCount);
    }

    public async Task<Dictionary<UserStatus, int>> GetCountByStatusAsync(CancellationToken ct = default)
    {
        var users = await dbContext.Users.ToListAsync(ct);
        return users
            .GroupBy(x => x.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task SoftDeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user != null)
        {
            user.Status = UserStatus.Deleted;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
