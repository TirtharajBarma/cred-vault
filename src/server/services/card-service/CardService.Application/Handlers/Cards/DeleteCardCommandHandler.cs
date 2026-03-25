using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Handlers.Cards;

public sealed class DeleteCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<DeleteCardCommand, OperationResult>
{
    public async Task<OperationResult> Handle(DeleteCardCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = "User is not authorized."
            };
        }

        if (request.CardId == Guid.Empty)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CardId is required."
            };
        }

        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);
        if (card is null)
        {
            return new OperationResult
            {
                Success = false,
                ErrorCode = ErrorCodes.CardNotFound,
                Message = "Card not found."
            };
        }

        await cardRepository.DeleteAsync(card, cancellationToken);

        return new OperationResult
        {
            Success = true,
            Message = "Card deleted successfully."
        };
    }
}
