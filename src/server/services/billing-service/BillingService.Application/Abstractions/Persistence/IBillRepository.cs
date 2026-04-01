using BillingService.Domain.Entities;

namespace BillingService.Application.Abstractions.Persistence;

public interface IBillRepository
{
    Task<Bill?> GetByIdAsync(Guid billId, CancellationToken cancellationToken = default);
    Task<Bill?> GetByIdAndUserIdAsync(Guid billId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<Bill>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPendingBillAsync(Guid userId, Guid cardId, CancellationToken cancellationToken = default);
    Task AddAsync(Bill bill, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default);
}
