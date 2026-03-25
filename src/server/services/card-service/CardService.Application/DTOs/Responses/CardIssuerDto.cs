namespace CardService.Application.DTOs.Responses;

public sealed class CardIssuerDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
