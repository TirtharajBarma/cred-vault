namespace Shared.Contracts.DTOs.Card.Responses;

public class CardResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public CardDto? Card { get; set; }
}
