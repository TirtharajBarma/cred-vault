using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BillingService.Domain.Entities;

public enum StatementStatus
{
    Generated = 0,
    Paid = 1,
    Overdue = 2,
    PartiallyPaid = 3
}

public class Statement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid CardId { get; set; }

    public Guid? BillId { get; set; }
    public Bill? Bill { get; set; }

    [Required]
    [MaxLength(100)]
    public string StatementPeriod { get; set; } = string.Empty;

    [Required]
    public DateTime PeriodStartUtc { get; set; }

    [Required]
    public DateTime PeriodEndUtc { get; set; }

    [Required]
    public DateTime GeneratedAtUtc { get; set; }

    public DateTime? DueDateUtc { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal TotalPurchases { get; set; }

    public decimal TotalPayments { get; set; }

    public decimal TotalRefunds { get; set; }

    public decimal PenaltyCharges { get; set; }

    public decimal InterestCharges { get; set; }

    public decimal ClosingBalance { get; set; }

    public decimal MinimumDue { get; set; }

    public decimal AmountPaid { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public StatementStatus Status { get; set; }

    public string CardLast4 { get; set; } = string.Empty;

    public string CardNetwork { get; set; } = string.Empty;

    public string IssuerName { get; set; } = string.Empty;

    public decimal CreditLimit { get; set; }

    public decimal AvailableCredit { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<StatementTransaction> Transactions { get; set; } = new List<StatementTransaction>();
}

public class StatementTransaction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid StatementId { get; set; }

    [ForeignKey("StatementId")]
    public Statement Statement { get; set; } = null!;

    public Guid? SourceTransactionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(256)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime DateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
