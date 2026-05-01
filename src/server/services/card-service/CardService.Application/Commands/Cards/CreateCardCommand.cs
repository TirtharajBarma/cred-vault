using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public sealed record CreateCardCommand(
    Guid UserId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    string CardNumber,
    Guid IssuerId,
    bool IsDefault,
    string EncryptedCardNumber,
    string? UserEmail = null) : IRequest<CardResult>;

public sealed class CreateCardCommandHandler(ICardRepository cards, ILogger<CreateCardCommandHandler> logger) : IRequestHandler<CreateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(CreateCardCommand request, CancellationToken ct)
    {
        logger.LogInformation("CreateCard requested: UserId={UserId}", request.UserId);

        if (request.UserId == Guid.Empty)
        {
            logger.LogWarning("CreateCard rejected: UserId is empty");
            return new() { Success = false, ErrorCode = "Forbidden", Message = "Not authorized" };
        }

        if (string.IsNullOrWhiteSpace(request.CardNumber) || string.IsNullOrWhiteSpace(request.CardholderName))
        {
            logger.LogWarning("CreateCard rejected: missing card number or holder name");
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Card number and holder name required" };
        }

        if (string.IsNullOrWhiteSpace(request.EncryptedCardNumber))
        {
            logger.LogWarning("CreateCard rejected: encrypted card number missing");
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Card number could not be secured" };
        }

        if (request.ExpMonth < 1 || request.ExpMonth > 12)
        {
            logger.LogWarning("CreateCard rejected: invalid exp month {ExpMonth}", request.ExpMonth);
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Expiration month must be between 1 and 12" };
        }

        var nowUtc = DateTime.UtcNow;
        if (request.ExpYear < nowUtc.Year || request.ExpYear > nowUtc.Year + 25)
        {
            logger.LogWarning("CreateCard rejected: invalid exp year {ExpYear}", request.ExpYear);
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Expiration year is invalid" };
        }

        var digits = CardHelpers.DigitsOnly(request.CardNumber);
        if (digits.Length < 13 || digits.Length > 19)
        {
            logger.LogWarning("CreateCard rejected: invalid card number length {Length}", digits.Length);
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Invalid card number length" };
        }

        if (!CardHelpers.IsValidLuhn(digits))
        {
            logger.LogWarning("CreateCard rejected: failed Luhn checksum validation");
            return new() { Success = false, ErrorCode = "ValidationError", Message = "Invalid card number" };
        }

        var issuer = await cards.GetIssuerByIdAsync(request.IssuerId, ct);
        if (issuer == null)
        {
            logger.LogWarning("CreateCard rejected: issuer {IssuerId} not found", request.IssuerId);
            return new() { Success = false, Message = "Issuer not found" };
        }

        var detectedNetwork = CardHelpers.DetectNetwork(digits);
        if (detectedNetwork != CardNetwork.Unknown && issuer.Network != detectedNetwork)
        {
            logger.LogWarning("CreateCard rejected: issuer/network mismatch");
            return new() { Success = false, Message = "Issuer doesn't support this card network" };
        }

        var network = issuer.Network;

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;     // extract the last 4 digit
        if (await cards.HasDuplicateCardAsync(request.UserId, network, last4, ct))
        {
            logger.LogWarning("CreateCard rejected: duplicate card for UserId={UserId}", request.UserId);
            return new() { Success = false, Message = "Duplicate card" };
        }

        var card = new CreditCard
        {
            Id = Guid.NewGuid(), UserId = request.UserId, CardholderName = request.CardholderName.Trim(),
            ExpMonth = request.ExpMonth, ExpYear = request.ExpYear, Last4 = last4,
            MaskedNumber = CardHelpers.MaskCardNumber(digits), IssuerId = issuer.Id,
            EncryptedCardNumber = request.EncryptedCardNumber,
            CreditLimit = 0, OutstandingBalance = 0, BillingCycleStartDay = DateTime.UtcNow.Day,
            IsDefault = request.IsDefault, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };

        if (card.IsDefault) await cards.UnsetDefaultForUserAsync(request.UserId, null, ct);
        await cards.AddAsync(card, ct);
        logger.LogInformation("Card created: {CardId}, UserId={UserId}, Last4={Last4} — pending admin approval", card.Id, request.UserId, last4);

        return new() { Success = true, Message = "Card created", Card = CardMapping.ToDto(card) };
    }
}
