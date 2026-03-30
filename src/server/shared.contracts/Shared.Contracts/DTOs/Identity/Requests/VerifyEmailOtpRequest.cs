namespace Shared.Contracts.DTOs.Identity.Requests;

public class VerifyEmailOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
