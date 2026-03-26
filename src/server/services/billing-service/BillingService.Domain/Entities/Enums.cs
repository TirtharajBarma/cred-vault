namespace BillingService.Domain.Entities;

public enum BillStatus
{
    Pending = 1,
    Paid = 2,
    Overdue = 3
}


public enum RewardTransactionType
{
    Earned = 1,
    Adjusted = 2,
    Redeemed = 3
}

public enum CardNetwork
{
    Unknown = 0,
    Visa = 1,
    Mastercard = 2
}
