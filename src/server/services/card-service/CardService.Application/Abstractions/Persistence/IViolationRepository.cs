using CardService.Domain.Entities;

namespace CardService.Application.Abstractions.Persistence;

public interface IViolationRepository
{
    Task<CardViolation?> GetByIdAsync(Guid violationId, CancellationToken cancellationToken = default);
    Task<CardViolation?> GetActiveViolationByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task<List<CardViolation>> GetViolationsByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task AddAsync(CardViolation violation, CancellationToken cancellationToken = default);
    Task UpdateAsync(CardViolation violation, CancellationToken cancellationToken = default);
}
