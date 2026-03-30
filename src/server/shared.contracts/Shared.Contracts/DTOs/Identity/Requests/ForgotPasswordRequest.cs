namespace Shared.Contracts.DTOs.Identity.Requests;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}
