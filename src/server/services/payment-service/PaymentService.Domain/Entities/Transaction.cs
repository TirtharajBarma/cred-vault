using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

public sealed class Transaction
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    
    // Navigation
    public Payment Payment { get; set; } = null!;
}
