namespace Shared.Contracts.DTOs.Identity.Responses;

public class UserResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public UserSummary? User { get; set; }
}
