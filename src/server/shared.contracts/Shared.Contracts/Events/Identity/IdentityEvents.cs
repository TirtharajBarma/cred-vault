namespace Shared.Contracts.Events.Identity;

public interface IUserRegistered
{
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    DateTime CreatedAtUtc { get; }
}

public interface IUserOtpGenerated
{
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    string OtpCode { get; }
    string Purpose { get; } // "EmailVerification", "PasswordReset", etc.
    DateTime ExpiresAtUtc { get; }
}

public interface IUserDeleted
{
    Guid UserId { get; }
    DateTime DeletedAtUtc { get; }
}
