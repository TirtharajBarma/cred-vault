using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Infrastructure.Persistence.Sql.Repositories;

public sealed class PaymentRepository(PaymentDbContext dbContext) : IPaymentRepository
{
    public async Task<Payment?> GetByIdAsync(Guid id)
    {
        return await dbContext.Payments
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<Payment>> GetByUserIdAsync(Guid userId)
    {
        return await dbContext.Payments
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddAsync(Payment payment)
    {
        await dbContext.Payments.AddAsync(payment);
    }

    public Task UpdateAsync(Payment payment)
    {
        dbContext.Payments.Update(payment);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Payment>> GetStuckPaymentsAsync(Guid userId, Guid billId, CancellationToken ct = default)
    {
        return await dbContext.Payments
            .Where(x => x.UserId == userId 
                && x.BillId == billId 
                && x.Status == PaymentStatus.Initiated 
                && x.OtpExpiresAtUtc < DateTime.UtcNow)
            .ToListAsync(ct);
    }
}

public sealed class TransactionRepository(PaymentDbContext dbContext) : ITransactionRepository
{
    public async Task<IEnumerable<Transaction>> GetByPaymentIdAsync(Guid paymentId)
    {
        return await dbContext.Transactions
            .Where(x => x.PaymentId == paymentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
    }

    public async Task AddAsync(Transaction transaction)
    {
        await dbContext.Transactions.AddAsync(transaction);
    }
}
