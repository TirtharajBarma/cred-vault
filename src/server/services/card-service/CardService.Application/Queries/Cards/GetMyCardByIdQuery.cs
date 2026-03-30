using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.DTOs.Card.Responses;
using MediatR;

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
            return new CardResult { Success = false, Message = "Card not found.", ErrorCode = "CardNotFound" };
        }

        return new CardResult { Success = true, Message = "Card fetched successfully.", Card = CardMapping.ToDto(card) };
    }
}
