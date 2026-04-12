namespace PaymentService.Domain.Enums;

public enum PaymentStatus
{
    Initiated = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Reversed = 4,
    Cancelled = 5,
    Expired = 6
}

public enum PaymentType
{
    Full = 0,
    Partial = 1,
    Scheduled = 2
}

public enum PaymentTransactionType
{
    Payment = 0,
    Reversal = 1
}
