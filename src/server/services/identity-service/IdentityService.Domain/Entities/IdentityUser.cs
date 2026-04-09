namespace IdentityService.Domain.Entities;

using IdentityService.Domain.Enums;

/// <summary>
/// User entity representing a registered user in the system.
/// Stores authentication data (email, password hash), verification status, role, and account status.
/// Used across IdentityService for all user-related operations.
/// </summary>
/// <remarks>
/// Key properties:
/// - Id: Unique GUID identifier
/// - Email: User's email (unique, normalized to lowercase)
/// - PasswordHash: BCrypt hashed password (nullable for Google SSO users)
/// - IsEmailVerified: Boolean flag for email verification
/// - EmailVerificationOtp: OTP code for email verification
/// - PasswordResetOtp: OTP code for password reset
/// - Status: Account status (Active, Suspended, Blocked, PendingVerification)
/// - Role: User role (User or Admin)
/// </remarks>
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
