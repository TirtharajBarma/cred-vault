using MediatR;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.Models;

namespace CardService.Application.Commands.Cards;

public record UpdateCardByAdminCommand(Guid CardId, string? CardholderName, decimal CreditLimit, decimal? OutstandingBalance, int? BillingCycleStartDay) : IRequest<ApiResponse<object>>;

public class UpdateCardByAdminCommandHandler(
    ICardRepository cardRepository) : IRequestHandler<UpdateCardByAdminCommand, ApiResponse<object>>
{
    public async Task<ApiResponse<object>> Handle(UpdateCardByAdminCommand request, CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(request.CardId, cancellationToken);
        if (card is null)
        {
            return new ApiResponse<object> { Success = false, Message = "Card not found." };
        }

        if (!string.IsNullOrWhiteSpace(request.CardholderName))
            card.CardholderName = request.CardholderName;

        if (request.CreditLimit > 0)
            card.CreditLimit = request.CreditLimit;

        if (request.OutstandingBalance.HasValue)
            card.OutstandingBalance = request.OutstandingBalance.Value;

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

        return new ApiResponse<object> { Success = true, Message = "Card updated successfully.", Data = CardMapping.ToDto(card) };
    }
}
