namespace Shared.Contracts.Events.Payment;

public interface IPaymentCompleted
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    Guid CardId { get; }
    Guid BillId { get; }
    decimal Amount { get; }
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

public interface IPaymentReversed
{
    Guid PaymentId { get; }
    Guid UserId { get; }
    Guid BillId { get; }
    Guid CardId { get; }
    decimal Amount { get; }
    decimal PointsDeducted { get; }
    DateTime ReversedAt { get; }
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
