using System;

namespace CardService.Domain.Entities;

public enum CardTransactionType
{
    Purchase = 0,
    Payment = 1,
    Refund = 2
}

public class CardTransaction
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public CreditCard? Card { get; set; }
    public Guid UserId { get; set; }
    public CardTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateUtc { get; set; }
}
