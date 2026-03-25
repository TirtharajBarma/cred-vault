using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Handlers.Cards;

public sealed class UpdateCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<UpdateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.CardholderName))
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CardholderName is required."
            };
        }

        if (!CardHelpers.IsValidExpiry(request.ExpMonth, request.ExpYear))
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Expiry month/year is invalid or already expired."
            };
        }

        if (!CardHelpers.IsValidBillingCycleStartDay(request.BillingCycleStartDay))
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "BillingCycleStartDay must be between 1 and 31."
            };
        }

        if (request.CreditLimit < 0)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CreditLimit must be greater than or equal to 0."
            };
        }

        if (request.OutstandingBalance < 0)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "OutstandingBalance must be greater than or equal to 0."
            };
        }

        if (request.OutstandingBalance > request.CreditLimit)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "OutstandingBalance cannot be greater than CreditLimit."
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

        card.CardholderName = request.CardholderName.Trim();
        card.ExpMonth = request.ExpMonth;
        card.ExpYear = request.ExpYear;
        card.CreditLimit = request.CreditLimit;
        card.OutstandingBalance = request.OutstandingBalance;
        card.BillingCycleStartDay = request.BillingCycleStartDay;
        card.IsDefault = request.IsDefault;
        card.UpdatedAtUtc = DateTime.UtcNow;

        if (card.IsDefault)
        {
            await cardRepository.UnsetDefaultForUserAsync(request.UserId, exceptCardId: card.Id, cancellationToken);
        }

        await cardRepository.UpdateAsync(card, cancellationToken);

        return new CardResult
        {
            Success = true,
            Message = "Card updated successfully.",
            Card = CardMapping.ToDto(card)
        };
    }
}
