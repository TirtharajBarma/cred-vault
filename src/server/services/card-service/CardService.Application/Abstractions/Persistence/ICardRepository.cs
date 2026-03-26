using CardService.Domain.Entities;

namespace CardService.Application.Abstractions.Persistence;

public interface ICardRepository
{
    Task AddAsync(CreditCard card, CancellationToken cancellationToken = default);
    Task<CreditCard?> GetByIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task<CreditCard?> GetByUserAndIdAsync(Guid userId, Guid cardId, CancellationToken cancellationToken = default);
    Task<List<CreditCard>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(CreditCard card, CancellationToken cancellationToken = default);
    Task DeleteAsync(CreditCard card, CancellationToken cancellationToken = default);
    Task UnsetDefaultForUserAsync(Guid userId, Guid? exceptCardId, CancellationToken cancellationToken = default);

    Task<CardIssuer?> GetIssuerByNetworkAsync(CardNetwork network, CancellationToken cancellationToken = default);
    Task<CardIssuer?> GetIssuerByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<CardIssuer>> ListIssuersAsync(CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateCardAsync(Guid userId, CardNetwork network, string last4, CancellationToken cancellationToken = default);
}
