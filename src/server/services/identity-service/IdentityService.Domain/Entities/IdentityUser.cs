namespace IdentityService.Domain.Entities;

using IdentityService.Domain.Enums;

public sealed class IdentityUser
{
	public Guid Id { get; set; }
	public string Email { get; set; } = string.Empty;
	public string FullName { get; set; } = string.Empty;
	public string? PasswordHash { get; set; }
	public bool IsEmailVerified { get; set; }
	public string? EmailVerificationOtp { get; set; }
	public DateTime? EmailVerificationOtpExpiresAtUtc { get; set; }
	public string? PasswordResetOtp { get; set; }
	public DateTime? PasswordResetOtpExpiresAtUtc { get; set; }
	public UserStatus Status { get; set; } = UserStatus.PendingVerification;
	public UserRole Role { get; set; } = UserRole.User;
	public DateTime CreatedAtUtc { get; set; }
	public DateTime UpdatedAtUtc { get; set; }
}
