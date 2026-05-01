using MediatR;
using MassTransit;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.Events.Card;
using Shared.Contracts.Models;

namespace CardService.Application.Commands.Cards;

public record UpdateCardByAdminCommand(Guid CardId, string? CardholderName, decimal CreditLimit, decimal? OutstandingBalance, int? BillingCycleStartDay) : IRequest<ApiResponse<object>>;

public class UpdateCardByAdminCommandHandler(
    ICardRepository cardRepository,
    IPublishEndpoint publisher) : IRequestHandler<UpdateCardByAdminCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(UpdateCardByAdminCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
        if (card is null)
        {
            return new ApiResponse<object> { Success = false, Message = "Card not found." };
        }

        var wasUnconfigured = card.CreditLimit <= 0;

        if (!string.IsNullOrWhiteSpace(request.CardholderName))
            card.CardholderName = request.CardholderName;

        if (request.CreditLimit > 0)            // only update the value if the new value is greater than 0
            card.CreditLimit = request.CreditLimit;     // change card limit to limit req coming from admin

        if (request.OutstandingBalance.HasValue)
            card.OutstandingBalance = request.OutstandingBalance.Value;

        // if admin is setting the billing cycle day, validate it and update only if valid
        if (request.BillingCycleStartDay.HasValue)
        {
            if (!CardHelpers.IsValidBillingCycleStartDay(request.BillingCycleStartDay.Value))
            {
                return new ApiResponse<object> { Success = false, Message = "Billing cycle day must be between 1 and 31." };
            }
            card.BillingCycleStartDay = request.BillingCycleStartDay.Value;
        }

        card.UpdatedAtUtc = DateTime.UtcNow;
        await cardRepository.UpdateAsync(card, cancellationToken);

        // Fire ICardAdded notification when admin first configures the card (credit limit > 0)
        // This is the "admin approval" event - user gets notified their card is active
        if (wasUnconfigured && card.CreditLimit > 0)
        {
            await publisher.Publish<ICardAdded>(new
            {
                CardId = card.Id,
                UserId = card.UserId,
                Email = string.Empty,   // Notification service will look up email by UserId
                FullName = card.CardholderName,
                CardNumberLast4 = card.Last4,
                CardHolderName = card.CardholderName,
                AddedAt = card.UpdatedAtUtc
            }, cancellationToken);
        }

        return new ApiResponse<object> { Success = true, Message = "Card updated successfully.", Data = CardMapping.ToDto(card) };
    }
}
