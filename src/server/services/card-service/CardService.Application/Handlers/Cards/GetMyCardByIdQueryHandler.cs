using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using CardService.Application.Queries.Cards;
using MediatR;

namespace CardService.Application.Handlers.Cards;

public sealed class GetMyCardByIdQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<GetMyCardByIdQuery, CardResult>
{
    public async Task<CardResult> Handle(GetMyCardByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = "User is not authorized."
            };
        }

        if (request.CardId == Guid.Empty)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CardId is required."
            };
        }

        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);
        if (card is null)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.CardNotFound,
                Message = "Card not found."
            };
        }

        return new CardResult
        {
            Success = true,
            Message = "Card fetched successfully.",
            Card = CardMapping.ToDto(card)
        };
    }
}
