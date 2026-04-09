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
