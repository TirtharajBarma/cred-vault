namespace PaymentService.Domain.Enums;

public enum PaymentStatus
{
    Initiated = 0,      // Payment created, OTP sent, awaiting verification
    Processing = 1,     // OTP verified, payment being processed
    Completed = 2,      // Payment successful
    Failed = 3,         // Payment failed (error during processing)
    Reversed = 4,       // Payment reversed (compensation)
    Cancelled = 5,      // Payment cancelled by user
    Expired = 6        // OTP expired, payment abandoned
}

public enum PaymentType
{
    Full = 0,           // Full bill amount payment
    Partial = 1,        // Partial payment (minimum due or custom amount)
    Scheduled = 2       // Future scheduled payment (NOT USED YET)
}

public enum TransactionType
{
    Payment = 0,       // Payment transaction record
    Reversal = 1       // Reversal transaction record
}
