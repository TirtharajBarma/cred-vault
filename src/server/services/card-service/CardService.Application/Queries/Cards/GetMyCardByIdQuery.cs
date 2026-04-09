using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.DTOs.Card.Responses;
using MediatR;
using Shared.Contracts.Exceptions;

namespace CardService.Application.Queries.Cards;

public sealed record GetMyCardByIdQuery(Guid UserId, Guid CardId) : IRequest<CardResult>;

public sealed class GetMyCardByIdQueryHandler(ICardRepository cardRepository)
    : IRequestHandler<GetMyCardByIdQuery, CardResult>
{
    public async Task<CardResult> Handle(GetMyCardByIdQuery request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);

        if (card is null)
        {
            throw new NotFoundException("Card", request.CardId);
        }

        return new CardResult { Success = true, Message = "Card fetched successfully.", Card = CardMapping.ToDto(card) };
    }
}
