namespace Shared.Contracts.DTOs.Card.Responses;

public class CardsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public List<CardDto> Cards { get; set; } = new();
}
