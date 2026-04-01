namespace Shared.Contracts.DTOs.Card.Responses;

public sealed class IssuersResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public List<CardIssuerDto> Issuers { get; set; } = [];
}
