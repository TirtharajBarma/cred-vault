using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

public sealed class FraudAlert
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PaymentId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public FraudAlertStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public Payment Payment { get; set; } = null!;
}
