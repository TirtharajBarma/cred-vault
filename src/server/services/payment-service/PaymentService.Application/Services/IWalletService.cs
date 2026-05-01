using PaymentService.Domain.Entities;

namespace PaymentService.Application.Services;

public interface IWalletService
{
    Task<UserWallet> CreateWalletAsync(Guid userId, CancellationToken ct = default);
    Task<UserWallet?> GetWalletAsync(Guid userId, CancellationToken ct = default);
    Task<decimal> TopUpAsync(Guid userId, decimal amount, string description, CancellationToken ct = default);
    Task<bool> DeductAsync(Guid userId, decimal amount, Guid? relatedPaymentId, string description, CancellationToken ct = default);
    Task<bool> RefundAsync(Guid userId, decimal amount, Guid? relatedPaymentId, string description, CancellationToken ct = default);
    Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid userId, int skip = 0, int take = 20, CancellationToken ct = default);
    Task<bool> HasBalanceAsync(Guid userId, decimal amount, CancellationToken ct = default);
}
