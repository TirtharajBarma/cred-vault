namespace CardService.Domain.Entities;

public sealed class CreditCard
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public Guid IssuerId { get; set; }
    public CardIssuer? Issuer { get; set; }

    public string CardholderName { get; set; } = string.Empty;

    public int ExpMonth { get; set; }
    public int ExpYear { get; set; }

    public string Last4 { get; set; } = string.Empty;
    public string MaskedNumber { get; set; } = string.Empty;
    public string? EncryptedCardNumber { get; set; }

    public decimal CreditLimit { get; set; }
    public decimal OutstandingBalance { get; set; }
    public int BillingCycleStartDay { get; set; }

    public bool IsDefault { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
