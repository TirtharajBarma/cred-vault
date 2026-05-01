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

public sealed class WalletRepository(PaymentDbContext dbContext) : IWalletRepository
{
    public async Task<UserWallet?> GetByUserIdAsync(Guid userId)
    {
        return await dbContext.UserWallets
            .FirstOrDefaultAsync(x => x.UserId == userId);
    }

    public async Task<UserWallet?> GetByIdAsync(Guid id)
    {
        return await dbContext.UserWallets
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid walletId, int skip = 0, int take = 20)
    {
        return await dbContext.WalletTransactions
            .Where(x => x.WalletId == walletId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
    }

    public async Task AddAsync(UserWallet wallet)
    {
        await dbContext.UserWallets.AddAsync(wallet);
    }

    public async Task UpdateAsync(UserWallet wallet)
    {
        dbContext.UserWallets.Update(wallet);
        await Task.CompletedTask;
    }

    public async Task<WalletTransaction?> GetTransactionByRelatedPaymentIdAsync(Guid relatedPaymentId)
    {
        return await dbContext.WalletTransactions
            .FirstOrDefaultAsync(x => x.RelatedPaymentId == relatedPaymentId);
    }

    public async Task AddTransactionAsync(WalletTransaction transaction)
    {
        await dbContext.WalletTransactions.AddAsync(transaction);
    }
}

public sealed class RazorpayWalletTopUpRepository(PaymentDbContext dbContext) : IRazorpayWalletTopUpRepository
{
    public async Task<RazorpayWalletTopUp?> GetByIdAsync(Guid id)
    {
        return await dbContext.RazorpayWalletTopUps
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<RazorpayWalletTopUp?> GetByOrderIdAsync(string orderId)
    {
        return await dbContext.RazorpayWalletTopUps
            .FirstOrDefaultAsync(x => x.RazorpayOrderId == orderId);
    }

    public async Task AddAsync(RazorpayWalletTopUp topUp)
    {
        await dbContext.RazorpayWalletTopUps.AddAsync(topUp);
    }

    public Task UpdateAsync(RazorpayWalletTopUp topUp)
    {
        dbContext.RazorpayWalletTopUps.Update(topUp);
        return Task.CompletedTask;
    }
}
