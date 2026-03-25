namespace IdentityService.Application.DTOs.Responses;

public class UserSummary
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
