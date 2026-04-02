using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Shared.Contracts.Events.Card;
using Shared.Contracts.Models;
using MediatR;
using MassTransit;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CardService.Application.Commands.Cards;

public sealed record CreateCardCommand(Guid UserId, string CardholderName, int ExpMonth, int ExpYear, string CardNumber, Guid IssuerId, bool IsDefault, string AuthorizationHeader) : IRequest<CardResult>;

public sealed class CreateCardCommandHandler(ICardRepository cards, IPublishEndpoint publisher, IHttpClientFactory http, IConfiguration config, ILogger<CreateCardCommandHandler> logger) : IRequestHandler<CreateCardCommand, CardResult>
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
            return new() { Success = false, Message = "Invalid card number length" };
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

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
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
            CreditLimit = 0, OutstandingBalance = 0, BillingCycleStartDay = DateTime.UtcNow.Day,
            IsDefault = request.IsDefault, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };

        if (card.IsDefault) await cards.UnsetDefaultForUserAsync(request.UserId, null, ct);
        await cards.AddAsync(card, ct);
        logger.LogInformation("Card created: {CardId}, UserId={UserId}, Last4={Last4}", card.Id, request.UserId, last4);

        var (ok, user, error) = await GetUserAsync(request.AuthorizationHeader, ct);
        var userEmail = ok && user != null ? user.Email : null;
        var userName = ok && user != null ? user.FullName : null;

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            logger.LogWarning("Could not fetch user email for {UserId}: {Error}. Publishing event with fallback.", request.UserId, error);
            userEmail = $"user-{request.UserId}@credvault.local";
            userName = request.CardholderName.Trim();
        }

        await publisher.Publish<ICardAdded>(new { CardId = card.Id, UserId = card.UserId, Email = userEmail, FullName = userName, CardNumberLast4 = card.Last4, CardHolderName = card.CardholderName, AddedAt = card.CreatedAtUtc }, ct);
        logger.LogInformation("Published ICardAdded for {CardId}", card.Id);

        return new() { Success = true, Message = "Card created", Card = CardMapping.ToDto(card) };
    }

    private async Task<(bool, UserDto?, string?)> GetUserAsync(string authHeader, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();
            client.BaseAddress = new Uri(config["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/");
            var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/identity/users/me");
            if (!string.IsNullOrEmpty(authHeader)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
            var resp = await client.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch /users/me: {Status}", resp.StatusCode);
                return (false, null, resp.ReasonPhrase);
            }

            var content = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<UserResponseWrapper>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Data?.User is null)
            {
                return (false, null, "User not found");
            }
            return (result.Success, result.Data.User, result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching /users/me");
            return (false, null, ex.Message);
        }
    }

    private class UserResponseWrapper
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public UserData? Data { get; set; }
    }

    private class UserData
    {
        public UserDto? User { get; set; }
    }

    private record UserDto(Guid Id, string Email, string FullName);
}
