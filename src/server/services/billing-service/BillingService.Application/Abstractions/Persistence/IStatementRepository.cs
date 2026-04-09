using BillingService.Domain.Entities;

namespace BillingService.Application.Abstractions.Persistence;

public interface IStatementRepository
{
    Task<Statement?> GetByIdAsync(Guid statementId, CancellationToken ct = default);
    Task<Statement?> GetByBillIdAsync(Guid billId, CancellationToken ct = default);
    Task<List<Statement>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Statement>> GetByCardIdAsync(Guid cardId, CancellationToken ct = default);
    Task<List<Statement>> GetAllAsync(CancellationToken ct = default);
    Task<List<StatementTransaction>> GetTransactionsAsync(Guid statementId, CancellationToken ct = default);
    Task<bool> ExistsForCardInPeriodAsync(Guid cardId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
    Task AddAsync(Statement statement, CancellationToken ct = default);
    Task AddTransactionsAsync(List<StatementTransaction> transactions, CancellationToken ct = default);
    Task UpdateAsync(Statement statement, CancellationToken ct = default);
}
