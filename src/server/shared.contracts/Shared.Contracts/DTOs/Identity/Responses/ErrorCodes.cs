namespace Shared.Contracts.DTOs.Identity.Responses;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidOtp = "INVALID_OTP";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string Forbidden = "FORBIDDEN";
}
