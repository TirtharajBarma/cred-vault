using MassTransit;

namespace PaymentService.Domain.Entities;

public sealed class PaymentSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public Guid PaymentId { get; set; }
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public Guid CardId { get; set; }
    public Guid BillId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentType { get; set; }

    public decimal RiskScore { get; set; }
    public string? RiskDecision { get; set; }

    // OTP — stored here so verify-otp can validate without Redis
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }

    public decimal RewardPointsGranted { get; set; }
    public string? CompensationReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
