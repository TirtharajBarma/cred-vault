namespace BillingService.Domain.Entities;

public sealed class RewardTier
{
    public Guid Id { get; set; }

    public CardNetwork CardNetwork { get; set; }
    public Guid? IssuerId { get; set; }

    public decimal MinSpend { get; set; }

    // Example: 0.015 means 1.5% rewards
    public decimal RewardRate { get; set; }

    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
