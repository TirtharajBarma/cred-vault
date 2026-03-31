namespace PaymentService.Domain.Enums;

public enum PaymentStatus
{
    Initiated,
    
    // Saga-internal state — tracks OTP requirement
    RiskCheckPassed,
    
    // Saga-internal state — tracks whether payment is sent to consumers
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

public enum RiskDecision
{
    AutoApproved,
    OTPRequired,
    Blocked
}

public enum TransactionType
{
    Payment,
    Reversal
}

public enum FraudAlertStatus
{
    Open,
    Resolved,
    FalsePositive
}
