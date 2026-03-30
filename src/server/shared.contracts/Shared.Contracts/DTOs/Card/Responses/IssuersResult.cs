namespace Shared.Contracts.DTOs.Card.Responses;

public sealed class IssuersResult : OperationResult
{
    public List<CardIssuerDto> Issuers { get; set; } = [];
}
