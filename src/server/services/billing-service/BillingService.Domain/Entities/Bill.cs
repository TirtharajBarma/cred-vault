namespace BillingService.Domain.Entities;

public sealed class Bill
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Cross-service reference (no FK constraint to CardService)
    public Guid CardId { get; set; }
    public CardNetwork CardNetwork { get; set; }
    public Guid IssuerId { get; set; }

    public decimal Amount { get; set; }
    public decimal MinDue { get; set; }
    public string Currency { get; set; } = "USD";

    public DateTime BillingDateUtc { get; set; }
    public DateTime DueDateUtc { get; set; }

    public BillStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
