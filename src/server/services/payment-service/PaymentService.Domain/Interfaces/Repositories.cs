using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<Payment>> GetStuckPaymentsAsync(Guid userId, Guid billId, CancellationToken ct = default);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
}

public interface ITransactionRepository
{
    Task<IEnumerable<Transaction>> GetByPaymentIdAsync(Guid paymentId);
    Task AddAsync(Transaction transaction);
}

public interface IWalletRepository
{
    Task<UserWallet?> GetByUserIdAsync(Guid userId);
    Task<UserWallet?> GetByIdAsync(Guid id);
    Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid walletId, int skip = 0, int take = 20);
    Task AddAsync(UserWallet wallet);
    Task UpdateAsync(UserWallet wallet);
    Task AddTransactionAsync(WalletTransaction transaction);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
