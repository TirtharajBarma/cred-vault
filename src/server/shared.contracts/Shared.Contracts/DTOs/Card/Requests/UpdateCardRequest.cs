namespace Shared.Contracts.DTOs.Card.Requests;

public sealed class UpdateCardRequest
{
    public string CardholderName { get; set; } = string.Empty;
    public int ExpMonth { get; set; }
    public int ExpYear { get; set; }
    public bool IsDefault { get; set; }
}
