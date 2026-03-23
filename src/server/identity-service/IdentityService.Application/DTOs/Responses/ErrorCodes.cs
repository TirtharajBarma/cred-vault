namespace IdentityService.Application.DTOs.Responses;

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string DuplicateEmail = "DUPLICATE_EMAIL";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvalidStatus = "INVALID_STATUS";
    public const string InvalidOtp = "INVALID_OTP";
    public const string OtpExpired = "OTP_EXPIRED";
    public const string EmailSendFailed = "EMAIL_SEND_FAILED";
    public const string Forbidden = "FORBIDDEN";
}
