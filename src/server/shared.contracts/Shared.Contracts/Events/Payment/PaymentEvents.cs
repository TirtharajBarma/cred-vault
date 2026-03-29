namespace Shared.Contracts.Events.Payment;

public interface IPaymentInitiated
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    Guid CardId { get; }
    Guid BillId { get; }
    decimal Amount { get; }
    string PaymentType { get; }
    DateTime CreatedAt { get; }
    int RiskScore { get; }
}

public interface IPaymentCompleted
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    Guid CardId { get; }
    Guid BillId { get; }
    decimal Amount { get; }
    decimal RiskScore { get; }
    string RiskDecision { get; }
    DateTime CompletedAt { get; }
}

public interface IPaymentFailed
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    decimal Amount { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface IFraudDetected
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    decimal RiskScore { get; }
    string AlertType { get; }
    DateTime DetectedAt { get; }
}

public interface IOTPVerified
{
    Guid PaymentId { get; }
    string OtpCode { get; }
    DateTime VerifiedAt { get; }
}

public interface IPaymentOtpGenerated
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    decimal Amount { get; }
    string OtpCode { get; }
    DateTime ExpiresAtUtc { get; }
}
