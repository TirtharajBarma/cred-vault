namespace Shared.Contracts.Events.Wallet;

public interface IWalletTopUpCompleted
{
    Guid WalletId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    decimal NewBalance { get; }
    DateTime CompletedAt { get; }
}

public interface IWalletDeducted
{
    Guid WalletId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    decimal NewBalance { get; }
    Guid? RelatedPaymentId { get; }
    DateTime DeductedAt { get; }
}

public interface IWalletRefunded
{
    Guid WalletId { get; }
    Guid UserId { get; }
    decimal Amount { get; }
    decimal NewBalance { get; }
    Guid? RelatedPaymentId { get; }
    DateTime RefundedAt { get; }
}

public interface IWalletInsufficientBalance
{
    Guid UserId { get; }
    decimal RequiredAmount { get; }
    decimal AvailableBalance { get; }
    Guid? RelatedPaymentId { get; }
    DateTime DetectedAt { get; }
}
