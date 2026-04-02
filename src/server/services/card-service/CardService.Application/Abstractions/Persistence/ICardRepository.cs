using CardService.Domain.Entities;
using Shared.Contracts.Enums;

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
    Task<CardIssuer?> GetIssuerByIdRawAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<CardIssuer>> ListIssuersAsync(CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateCardAsync(Guid userId, CardNetwork network, string last4, CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateIssuerAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task AddIssuerAsync(CardIssuer issuer, CancellationToken cancellationToken = default);
    Task UpdateIssuerAsync(CardIssuer issuer, CancellationToken cancellationToken = default);
    Task DeleteIssuerAsync(CardIssuer issuer, CancellationToken cancellationToken = default);

    Task AddTransactionAsync(CardTransaction transaction, CancellationToken cancellationToken = default);
    Task<List<CardTransaction>> GetTransactionsByCardAndUserAsync(Guid cardId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<CardTransaction>> GetTransactionsByCardIdAsync(Guid cardId, CancellationToken cancellationToken = default);
    Task<List<CardTransaction>> GetTransactionsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasDuplicateTransactionAsync(Guid cardId, TransactionType type, decimal amount, string description, DateTime dateUtc, CancellationToken cancellationToken = default);
    Task<bool> HasCardsByIssuerAsync(Guid issuerId, CancellationToken cancellationToken = default);

    Task<List<CreditCard>> GetBlockedCardsAsync(CancellationToken cancellationToken = default);
    Task<List<CreditCard>> GetAllActiveCardsWithBalanceAsync(CancellationToken cancellationToken = default);
    Task<bool> HasTransactionsAsync(Guid cardId, CancellationToken cancellationToken = default);
}
