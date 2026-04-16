namespace PaymentService.Domain.Entities;

public sealed class UserWallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalTopUps { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}
