using MassTransit;

namespace PaymentService.Domain.Entities;

public sealed class PaymentOrchestrationSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = "Initial";

    public Guid PaymentId { get; set; }
    public Guid UserId { get; set; }
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public Guid CardId { get; set; }
    public Guid BillId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentType { get; set; }

    public decimal RewardsAmount { get; set; }
    public bool RewardsRedeemed { get; set; }

    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }
    public bool OtpVerified { get; set; }

    public bool PaymentProcessed { get; set; }
    public bool BillUpdated { get; set; }
    public bool CardDeducted { get; set; }

    public string? PaymentError { get; set; }
    public string? BillUpdateError { get; set; }
    public string? CardDeductionError { get; set; }

    public string? CompensationReason { get; set; }
    public int CompensationAttempts { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
