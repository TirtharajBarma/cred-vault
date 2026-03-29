namespace CardService.Application.DTOs.Responses;

public class OperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
}
