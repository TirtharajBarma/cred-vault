namespace Shared.Contracts.DTOs.Card.Requests;

public sealed class CreateCardRequest
{
    public string CardholderName { get; set; } = string.Empty;
    public int ExpMonth { get; set; }
    public int ExpYear { get; set; }
    public string CardNumber { get; set; } = string.Empty;
    public Guid IssuerId { get; set; }
    public bool IsDefault { get; set; }
}
