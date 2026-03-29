using CardService.Application.DTOs.Responses;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;

namespace CardService.Application.Common;

public static class CardMapping
{
    public static CardDto ToDto(CreditCard card)
    {
        return new CardDto
        {
            Id = card.Id,
            UserId = card.UserId,
            IssuerId = card.IssuerId,
            IssuerName = card.Issuer?.Name ?? string.Empty,
            Network = (card.Issuer?.Network ?? CardNetwork.Unknown).ToString(),
            CardholderName = card.CardholderName,
            ExpMonth = card.ExpMonth,
            ExpYear = card.ExpYear,
            Last4 = card.Last4,
            MaskedNumber = card.MaskedNumber,
            CreditLimit = card.CreditLimit,
            OutstandingBalance = card.OutstandingBalance,
            BillingCycleStartDay = card.BillingCycleStartDay,
            IsDefault = card.IsDefault,
            IsVerified = card.IsVerified,
            CreatedAtUtc = card.CreatedAtUtc,
            UpdatedAtUtc = card.UpdatedAtUtc
        };
    }
}
