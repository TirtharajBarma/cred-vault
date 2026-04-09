namespace BillingService.Domain.Entities;

public enum BillStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4,
    PartiallyPaid = 5
}


public enum RewardTransactionType
{
    Earned = 1,
    Adjusted = 2,
    Redeemed = 3,
    Reversed = 4
}

