using IdentityService.Domain.Entities;

namespace IdentityService.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task AddAsync(IdentityUser user, CancellationToken cancellationToken = default);
    Task<IdentityUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateAsync(IdentityUser user, CancellationToken cancellationToken = default);
}
