using System;

namespace CardService.Domain.Entities;

public enum TransactionType
{
    Purchase = 1,
    Payment = 2,
    Refund = 3
}

public class CardTransaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public Guid UserId { get; set; }
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; }
}
