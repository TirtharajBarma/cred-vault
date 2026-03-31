namespace IdentityService.Domain.Enums;

public enum UserStatus
{
    PendingVerification = 1,
    Active = 2,
    Suspended = 3,
    Blocked = 4,
    Deleted = 5
}
