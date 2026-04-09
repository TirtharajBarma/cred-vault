using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence.Sql;

/// <summary>
/// SQL Server implementation of IUserRepository using Entity Framework Core.
/// Provides database operations for IdentityUser entities.
/// Uses IdentityDbContext for database access and tracks changes automatically.
/// </summary>
public sealed class SqlUserRepository(IdentityDbContext dbContext) : IUserRepository
{
    /// <summary>
    /// Adds a new user to the database.
    /// Saves immediately after adding to ensure persistence.
    /// </summary>
    public async Task AddAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by ID using FirstOrDefault (returns null if not found).
    /// </summary>
    public Task<IdentityUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    /// <summary>
    /// Retrieves a user by email address.
    /// Email comparison is done exactly as stored (should be normalized to lowercase on creation).
    /// </summary>
    public Task<IdentityUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return dbContext.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    /// <summary>
    /// Updates an existing user in the database.
    /// Uses EF Core change tracking - marks entity as modified.
    /// </summary>
    public async Task UpdateAsync(IdentityUser user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Update(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Lists users with pagination support.
    /// Supports optional search (matches fullName or email) and status filter.
    /// Returns users ordered by CreatedAtUtc descending (newest first).
    /// </summary>
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

    /// <summary>
    /// Gets count of users grouped by their status.
    /// Useful for admin dashboard statistics.
    /// </summary>
    public async Task<Dictionary<UserStatus, int>> GetCountByStatusAsync(CancellationToken ct = default)
    {
        return await dbContext.Users
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }
}
