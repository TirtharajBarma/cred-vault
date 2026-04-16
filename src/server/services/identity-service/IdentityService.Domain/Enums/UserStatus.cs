namespace IdentityService.Domain.Enums;

public enum UserStatus
{
    PendingVerification = 0,
    Active = 1,
    Suspended = 2,
    Blocked = 3,
    Deleted = 4
}
