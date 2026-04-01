using Shared.Contracts.Enums;
namespace CardService.Domain.Entities;

public sealed class CardIssuer
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public CardNetwork Network { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
