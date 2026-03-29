namespace CardService.Application.DTOs.Requests;

public sealed class CreateIssuerRequest
{
    public string Name { get; set; } = string.Empty;

    // Accepts "Visa" / "Mastercard" (case-insensitive) or their numeric enum values.
    public string Network { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
