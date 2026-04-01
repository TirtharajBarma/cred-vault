using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Interfaces;

public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(Guid id);
    Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId);
    Task AddAsync(Payment payment);
    Task UpdateAsync(Payment payment);
}

public interface ITransactionRepository
{
    Task<IEnumerable<Transaction>> GetByPaymentIdAsync(Guid paymentId);
    Task AddAsync(Transaction transaction);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
