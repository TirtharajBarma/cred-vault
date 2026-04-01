using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs;
using MediatR;

namespace CardService.Application.Commands.Cards;

public sealed record DeleteCardCommand(Guid UserId, Guid CardId) : IRequest<OperationResult>;

public sealed class DeleteCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<DeleteCardCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeleteCardCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);

        if (card is null)
        {
            return new OperationResult { Success = false, Message = "Card not found.", ErrorCode = "CardNotFound" };
        }

        if (card.OutstandingBalance > 0)
        {
            return new OperationResult { Success = false, Message = "Cannot delete card with outstanding balance. Pay off the balance first.", ErrorCode = "CardHasBalance" };
        }

        var hasTransactions = await cardRepository.HasTransactionsAsync(request.CardId, cancellationToken);
        if (hasTransactions)
        {
            return new OperationResult { Success = false, Message = "Cannot delete card that has transaction history.", ErrorCode = "CardHasTransactions" };
        }

        await cardRepository.DeleteAsync(card, cancellationToken);

        return new OperationResult { Success = true, Message = "Card deleted successfully." };
    }
}
