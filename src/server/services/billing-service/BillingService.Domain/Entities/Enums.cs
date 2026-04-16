namespace BillingService.Domain.Entities;

public enum BillStatus
{
    Pending = 0,
    Paid = 1,
    Overdue = 2,
    Cancelled = 3,
    PartiallyPaid = 4
}


public enum RewardTransactionType
{
    Earned = 0,
    Adjusted = 1,
    Redeemed = 2,
    Reversed = 3
}

