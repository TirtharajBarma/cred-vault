using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

public sealed class RiskScore
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Score { get; set; }
    public RiskDecision Decision { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Payment Payment { get; set; } = null!;
}
