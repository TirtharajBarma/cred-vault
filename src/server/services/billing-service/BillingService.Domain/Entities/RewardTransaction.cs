namespace BillingService.Domain.Entities;

public sealed class RewardTransaction
{
    public Guid Id { get; set; }
    public Guid RewardAccountId { get; set; }

    public Guid? BillId { get; set; }

    public decimal Points { get; set; }
    public RewardTransactionType Type { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
}
