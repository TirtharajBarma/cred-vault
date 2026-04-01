namespace CardService.Domain.Entities;

public enum ViolationType
{
    LatePayment = 1,
    OverdueBill = 2,
    MissedPayment = 3
}

public class CardViolation
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public CreditCard? Card { get; set; }
    public Guid UserId { get; set; }
    public Guid? BillId { get; set; }
    public ViolationType Type { get; set; }
    public int StrikeCount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal PenaltyAmount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AppliedAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
}
