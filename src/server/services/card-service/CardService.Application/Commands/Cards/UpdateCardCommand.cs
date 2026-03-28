using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Commands.Cards;

public record UpdateCardCommand(
    Guid UserId,
    Guid CardId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    decimal CreditLimit,
    decimal OutstandingBalance,
    int BillingCycleStartDay,
    bool IsDefault) : IRequest<CardResult>;

public sealed class UpdateCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<UpdateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByUserAndIdAsync(request.UserId, request.CardId, cancellationToken);
        if (card is null) return new CardResult { Success = false, Message = "Card not found." };

        card.CardholderName = request.CardholderName.Trim();
        card.ExpMonth = request.ExpMonth;
        card.ExpYear = request.ExpYear;
        card.CreditLimit = request.CreditLimit;
        card.OutstandingBalance = request.OutstandingBalance;
        card.BillingCycleStartDay = request.BillingCycleStartDay;
        card.IsDefault = request.IsDefault;
        card.UpdatedAtUtc = DateTime.UtcNow;

        if (card.IsDefault) await cardRepository.UnsetDefaultForUserAsync(request.UserId, card.Id, cancellationToken);
        await cardRepository.UpdateAsync(card, cancellationToken);

        return new CardResult { Success = true, Message = "Card updated.", Card = CardMapping.ToDto(card) };
    }
}
