using Shared.Contracts.DTOs.Card.Responses;
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
            AvailableCredit = card.CreditLimit - card.OutstandingBalance,
            BillingCycleStartDay = card.BillingCycleStartDay,
            IsDefault = card.IsDefault,
            IsVerified = card.IsVerified,
            IsDeleted = card.IsDeleted,
            CreatedAtUtc = card.CreatedAtUtc,
            UpdatedAtUtc = card.UpdatedAtUtc
        };
    }

    public static CardTransactionDto ToDto(CardTransaction transaction)
    {
        return new CardTransactionDto
        {
            Id = transaction.Id,
            CardId = transaction.CardId,
            UserId = transaction.UserId,
            Type = transaction.Type.ToString(),
            Amount = transaction.Amount,
            Description = transaction.Description,
            DateUtc = transaction.DateUtc
        };
    }
}
