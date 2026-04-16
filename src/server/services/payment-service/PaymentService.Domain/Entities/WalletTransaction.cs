using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

public sealed class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Description { get; set; }
    public Guid? RelatedPaymentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public UserWallet Wallet { get; set; } = null!;
}
