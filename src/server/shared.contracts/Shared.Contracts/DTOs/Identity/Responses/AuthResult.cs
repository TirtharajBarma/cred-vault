using System.Text.Json.Serialization;

namespace Shared.Contracts.DTOs.Identity.Responses;

public class AuthResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    
    [JsonPropertyName("user")]
    public UserSummary? User { get; set; }
}
