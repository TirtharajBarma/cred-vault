using CardService.Application.Abstractions.Persistence;
using Shared.Contracts.DTOs.Card.Responses;
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

        await cardRepository.DeleteAsync(card, cancellationToken);

        return new OperationResult { Success = true, Message = "Card deleted successfully." };
    }
}
