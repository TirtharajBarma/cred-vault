namespace Shared.Contracts.DTOs.Identity.Responses;

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? AccessToken { get; set; }
    public UserSummary? User { get; set; }
}
