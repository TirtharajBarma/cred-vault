using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.DTOs.Card.Responses;
using MediatR;
using Shared.Contracts.Exceptions;

namespace CardService.Application.Commands.Cards;

public record UpdateCardCommand(
    Guid UserId,
    Guid CardId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    bool IsDefault) : IRequest<CardResult>;

public sealed class UpdateCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<UpdateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);
        if (card is null) throw new NotFoundException("Card", request.CardId);

        card.CardholderName = request.CardholderName.Trim();
        card.ExpMonth = request.ExpMonth;
        card.ExpYear = request.ExpYear;
        card.IsDefault = request.IsDefault;
        card.UpdatedAtUtc = DateTime.UtcNow;

        if (card.IsDefault) await cardRepository.UnsetDefaultForUserAsync(request.UserId, card.Id, cancellationToken);
        await cardRepository.UpdateAsync(card, cancellationToken);

        return new CardResult { Success = true, Message = "Card updated.", Card = CardMapping.ToDto(card) };
    }
}
