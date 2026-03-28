using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Queries.Cards;

public sealed record ListMyCardsQuery(Guid UserId) : IRequest<CardsResult>;

public sealed class ListMyCardsQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<ListMyCardsQuery, CardsResult>
{
    public async Task<CardsResult> Handle(ListMyCardsQuery request, CancellationToken cancellationToken)
    {
        var cards = await cardRepository.ListByUserIdAsync(request.UserId, cancellationToken);

        return new CardsResult
        {
            Success = true,
            Message = "Cards fetched successfully.",
            Cards = cards.Select(CardMapping.ToDto).ToList()
        };
    }
}
