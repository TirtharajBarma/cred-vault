namespace Shared.Contracts.DTOs.Card.Requests;

public sealed class CreateIssuerRequest
{
    public string Name { get; set; } = string.Empty;
    public string Network { get; set; } = string.Empty;
}
