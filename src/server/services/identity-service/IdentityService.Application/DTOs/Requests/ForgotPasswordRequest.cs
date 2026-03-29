namespace IdentityService.Application.DTOs.Requests;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
