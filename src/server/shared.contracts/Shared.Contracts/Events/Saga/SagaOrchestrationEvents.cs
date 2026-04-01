namespace Shared.Contracts.Events.Saga;

public interface IStartPaymentOrchestration
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    Guid CardId { get; }
    Guid BillId { get; }
    decimal Amount { get; }
    string PaymentType { get; }
    string OtpCode { get; }
    DateTime StartedAt { get; }
}

public interface IBillOverdueDetected
{
    Guid BillId { get; }
    Guid CardId { get; }
    Guid UserId { get; }
    decimal OverdueAmount { get; }
    DateTime DueDate { get; }
    int DaysOverdue { get; }
    DateTime DetectedAt { get; }
}

public interface ICardBlocked
{
    Guid CardId { get; }
    Guid UserId { get; }
    int StrikeCount { get; }
    string Reason { get; }
    DateTime BlockedAt { get; }
}

public interface IOtpVerified
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    string OtpCode { get; }
    DateTime VerifiedAt { get; }
}

public interface IOtpFailed
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface IPaymentProcessRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    DateTime RequestedAt { get; }
}

public interface IPaymentProcessSucceeded
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    DateTime SucceededAt { get; }
}

public interface IPaymentProcessFailed
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface IBillUpdateRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    Guid BillId { get; }
    Guid CardId { get; }
    decimal Amount { get; }
    DateTime RequestedAt { get; }
}

public interface IBillUpdateSucceeded
{
    Guid CorrelationId { get; }
    Guid BillId { get; }
    Guid CardId { get; }
    DateTime SucceededAt { get; }
}

public interface IBillUpdateFailed
{
    Guid CorrelationId { get; }
    Guid BillId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface ICardDeductionRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    Guid CardId { get; }
    decimal Amount { get; }
    DateTime RequestedAt { get; }
}

public interface ICardDeductionSucceeded
{
    Guid CorrelationId { get; }
    Guid CardId { get; }
    decimal NewBalance { get; }
    DateTime SucceededAt { get; }
}

public interface ICardDeductionFailed
{
    Guid CorrelationId { get; }
    Guid CardId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface IRevertBillUpdateRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    Guid BillId { get; }
    decimal Amount { get; }
    DateTime RequestedAt { get; }
}

public interface IRevertBillUpdateSucceeded
{
    Guid CorrelationId { get; }
    Guid BillId { get; }
    DateTime SucceededAt { get; }
}

public interface IRevertBillUpdateFailed
{
    Guid CorrelationId { get; }
    Guid BillId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}

public interface IRevertPaymentRequested
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    Guid UserId { get; }
    Guid BillId { get; }
    Guid CardId { get; }
    decimal Amount { get; }
    DateTime RequestedAt { get; }
}

public interface IRevertPaymentSucceeded
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    DateTime SucceededAt { get; }
}

public interface IRevertPaymentFailed
{
    Guid CorrelationId { get; }
    Guid PaymentId { get; }
    string Reason { get; }
    DateTime FailedAt { get; }
}
