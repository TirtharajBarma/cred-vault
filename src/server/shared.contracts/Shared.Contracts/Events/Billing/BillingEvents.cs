namespace Shared.Contracts.Events.Billing;

public interface IBillGenerated
{
    Guid BillId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    Guid CardId { get; }
    decimal Amount { get; }
    DateTime DueDate { get; }
    DateTime GeneratedAt { get; }
}

public interface IBillDueReminder
{
    Guid BillId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    decimal Amount { get; }
    DateTime DueDate { get; }
    int DaysUntilDue { get; }
}

public interface IRewardEarned
{
    Guid UserId { get; }
    Guid BillId { get; }
    decimal PaymentAmount { get; }
    int PointsEarned { get; }
    DateTime EarnedAt { get; }
}
