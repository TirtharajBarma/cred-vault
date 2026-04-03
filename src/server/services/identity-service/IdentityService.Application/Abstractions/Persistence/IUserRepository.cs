using IdentityService.Domain.Entities;
using IdentityService.Domain.Enums;

namespace IdentityService.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task AddAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<IdentityUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<(List<IdentityUser> Users, int TotalCount)> ListAllAsync(int page, int pageSize, string? search, UserStatus? status, CancellationToken ct = default);
    Task<Dictionary<UserStatus, int>> GetCountByStatusAsync(CancellationToken ct = default);
}
