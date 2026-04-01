namespace PaymentService.Domain.Enums;

public enum PaymentStatus
{
    Initiated,
    Processing,
    Completed,
    Failed,
    Reversed,
    Cancelled
}

public enum PaymentType
{
    Full,
    Partial,
    Scheduled
}

public enum TransactionType
{
    Payment,
    Reversal
}
