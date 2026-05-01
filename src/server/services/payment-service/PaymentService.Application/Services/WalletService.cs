using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Services;

public sealed class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IWalletRepository walletRepository, IUnitOfWork unitOfWork, ILogger<WalletService> logger)
    {
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<UserWallet> CreateWalletAsync(Guid userId, CancellationToken ct = default)
    {
        var existingWallet = await _walletRepository.GetByUserIdAsync(userId);
        if (existingWallet != null)
        {
            return existingWallet;
        }

        var wallet = new UserWallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _walletRepository.AddAsync(wallet);
        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Created wallet for user {UserId}", userId);
        return wallet;
    }

    public async Task<UserWallet?> GetWalletAsync(Guid userId, CancellationToken ct = default)
    {
        return await _walletRepository.GetByUserIdAsync(userId);
    }

    public async Task<decimal> TopUpAsync(Guid userId, decimal amount, string description, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Top-up amount must be positive", nameof(amount));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            wallet = await CreateWalletAsync(userId, ct);
        }

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = WalletTransactionType.TopUp,
            Amount = amount,
            BalanceAfter = wallet.Balance + amount,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow
        };

        wallet.Balance += amount;
        wallet.TotalTopUps += amount;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        await _walletRepository.UpdateAsync(wallet);
        await _walletRepository.AddTransactionAsync(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Topped up wallet for user {UserId} with amount {Amount}. New balance: {Balance}", 
            userId, amount, wallet.Balance);

        return wallet.Balance;
    }

    public async Task<bool> DeductAsync(Guid userId, decimal amount, Guid? relatedPaymentId, string description, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Deduct amount must be positive", nameof(amount));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null || wallet.Balance < amount)
        {
            _logger.LogWarning("Insufficient balance for user {UserId}. Balance: {Balance}, Required: {Amount}",
                userId, wallet?.Balance ?? 0, amount);
            return false;
        }

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = WalletTransactionType.Payment,
            Amount = amount,
            BalanceAfter = wallet.Balance - amount,
            Description = description,
            RelatedPaymentId = relatedPaymentId,
            CreatedAtUtc = DateTime.UtcNow
        };

        wallet.Balance -= amount;
        wallet.TotalSpent += amount;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        await _walletRepository.UpdateAsync(wallet);
        await _walletRepository.AddTransactionAsync(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deducted {Amount} from wallet for user {UserId}. New balance: {Balance}",
            amount, userId, wallet.Balance);

        return true;
    }

    public async Task<bool> RefundAsync(Guid userId, decimal amount, Guid? relatedPaymentId, string description, CancellationToken ct = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Refund amount must be positive", nameof(amount));
        }

        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            wallet = await CreateWalletAsync(userId, ct);
        }

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = WalletTransactionType.Refund,
            Amount = amount,
            BalanceAfter = wallet.Balance + amount,
            Description = description,
            RelatedPaymentId = relatedPaymentId,
            CreatedAtUtc = DateTime.UtcNow
        };

        wallet.Balance += amount;
        wallet.UpdatedAtUtc = DateTime.UtcNow;

        await _walletRepository.UpdateAsync(wallet);
        await _walletRepository.AddTransactionAsync(transaction);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Refunded {Amount} to wallet for user {UserId}. New balance: {Balance}",
            amount, userId, wallet.Balance);

        return true;
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsAsync(Guid userId, int skip = 0, int take = 20, CancellationToken ct = default)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        if (wallet == null)
        {
            return Enumerable.Empty<WalletTransaction>();
        }

        return await _walletRepository.GetTransactionsAsync(wallet.Id, skip, take);
    }

    public async Task<bool> HasBalanceAsync(Guid userId, decimal amount, CancellationToken ct = default)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(userId);
        return wallet != null && wallet.Balance >= amount;
    }
}
