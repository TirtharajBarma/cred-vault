namespace IdentityService.Application.DTOs.Responses;

public class UserResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public UserSummary? User { get; set; }
}
