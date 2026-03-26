using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using CardService.Domain.Entities;
using MediatR;

namespace CardService.Application.Handlers.Cards;

public sealed class CreateCardCommandHandler(ICardRepository cardRepository)
    : IRequestHandler<CreateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(CreateCardCommand request, CancellationToken cancellationToken)
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

        if (string.IsNullOrWhiteSpace(request.CardholderName) || string.IsNullOrWhiteSpace(request.CardNumber))
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CardholderName and CardNumber are required."
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

        var digits = CardHelpers.DigitsOnly(request.CardNumber);
        if (digits.Length < 12 || digits.Length > 19 || !CardHelpers.PassesLuhn(digits))
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "CardNumber is invalid."
            };
        }

        var network = CardHelpers.DetectNetwork(digits);
        if (network == CardNetwork.Unknown)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Only Visa and Mastercard card numbers are supported."
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

        if (request.IssuerId == Guid.Empty)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "IssuerId is required."
            };
        }

        var issuer = await cardRepository.GetIssuerByIdAsync(request.IssuerId, cancellationToken);
        if (issuer is null)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "Selected card issuer was not found."
            };
        }

        if (issuer.Network != network)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = $"The card number provided is a {network} card, but the selected issuer ({issuer.Name}) expects a {issuer.Network} card."
            };
        }

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        var now = DateTime.UtcNow;

        var isDuplicate = await cardRepository.HasDuplicateCardAsync(request.UserId, network, last4, cancellationToken);
        if (isDuplicate)
        {
            return new CardResult
            {
                Success = false,
                ErrorCode = ErrorCodes.ValidationError,
                Message = "A card with this network and last 4 digits already exists in your account."
            };
        }

        var card = new CreditCard
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardholderName = request.CardholderName.Trim(),
            ExpMonth = request.ExpMonth,
            ExpYear = request.ExpYear,
            Last4 = last4,
            MaskedNumber = CardHelpers.MaskCardNumber(digits),
            IssuerId = issuer.Id,
            Issuer = issuer,
            CreditLimit = request.CreditLimit,
            OutstandingBalance = request.OutstandingBalance,
            BillingCycleStartDay = request.BillingCycleStartDay,
            IsDefault = request.IsDefault,
            IsVerified = false,
            VerifiedAtUtc = null,
            IsDeleted = false,
            DeletedAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        if (card.IsDefault)
        {
            await cardRepository.UnsetDefaultForUserAsync(request.UserId, exceptCardId: null, cancellationToken);
        }

        await cardRepository.AddAsync(card, cancellationToken);

        return new CardResult
        {
            Success = true,
            Message = "Card saved successfully.",
            Card = CardMapping.ToDto(card)
        };
    }
}
