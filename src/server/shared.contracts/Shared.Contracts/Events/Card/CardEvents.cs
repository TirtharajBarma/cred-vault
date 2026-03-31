namespace Shared.Contracts.Events.Card;

public interface ICardAdded
{
    Guid CardId { get; }
    Guid UserId { get; }
    string Email { get; }
    string FullName { get; }
    string CardNumberLast4 { get; }
    string CardHolderName { get; }
    DateTime AddedAt { get; }
}
