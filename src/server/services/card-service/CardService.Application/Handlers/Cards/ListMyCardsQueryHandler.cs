using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using CardService.Application.Queries.Cards;
using MediatR;

namespace CardService.Application.Handlers.Cards;

public sealed class ListMyCardsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListMyCardsQuery, CardsResult>
{
    public async Task<CardsResult> Handle(ListMyCardsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return new CardsResult
            {
                Success = false,
                ErrorCode = ErrorCodes.Forbidden,
                Message = "User is not authorized."
            };
        }

        var cards = await cardRepository.ListByUserIdAsync(request.UserId, cancellationToken);

        return new CardsResult
        {
            Success = true,
            Message = "Cards fetched successfully.",
            Cards = cards.Select(CardMapping.ToDto).ToList()
        };
    }
}
