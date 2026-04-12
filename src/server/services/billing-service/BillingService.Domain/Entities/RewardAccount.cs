namespace BillingService.Domain.Entities;

public sealed class RewardAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public Guid? RewardTierId { get; set; }
    public RewardTier? RewardTier { get; set; }

    public decimal PointsBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
