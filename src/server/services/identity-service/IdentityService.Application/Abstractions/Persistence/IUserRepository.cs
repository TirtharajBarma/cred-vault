using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.Abstractions.Persistence;

/// <summary>
/// Repository interface for user data access operations.
/// Defines contract for CRUD operations on IdentityUser entities.
/// Implementation is provided by SqlUserRepository using Entity Framework Core.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Creates a new user in the database.
    /// </summary>
    /// <param name="user">IdentityUser entity to create (Id should be generated beforehand)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(IdentityUser user, CancellationToken cancellationToken = default);
    //! IdentityUser -> user domain

    /// <summary>
    /// Retrieves a user by their unique identifier.
    /// </summary>
    /// <param name="userId">User's GUID identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IdentityUser if found, null otherwise</returns>
    //! <T> -> returns
    Task<IdentityUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their email address (case-insensitive).
    /// </summary>
    /// <param name="email">User's email (will be normalized to lowercase)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>IdentityUser if found, null otherwise</returns>
    Task<IdentityUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the database.
    /// </summary>
    /// <param name="user">IdentityUser entity with updated properties</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAsync(IdentityUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users with pagination and optional search/filter.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="search">Optional search term (matches email or fullName)</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple of (list of users, total count matching criteria)</returns>
    Task<(List<IdentityUser> Users, int TotalCount)> ListAllAsync(int page, int pageSize, string? search, UserStatus? status, CancellationToken ct = default);

    /// <summary>
    /// Gets count of users grouped by status (for admin dashboard).
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Dictionary mapping UserStatus to count</returns>
    Task<Dictionary<UserStatus, int>> GetCountByStatusAsync(CancellationToken ct = default);
}
