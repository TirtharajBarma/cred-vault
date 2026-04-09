using PaymentService.Domain.Enums;

namespace PaymentService.Domain.Entities;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public Guid BillId { get; set; }
    public decimal Amount { get; set; }
    public PaymentType PaymentType { get; set; }
    public PaymentStatus Status { get; set; }
    public string? FailureReason { get; set; }
    
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAtUtc { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
