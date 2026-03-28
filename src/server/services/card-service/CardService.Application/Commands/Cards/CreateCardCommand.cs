using CardService.Application.Abstractions.Persistence;
using CardService.Application.Common;
using CardService.Application.DTOs.Responses;
using CardService.Domain.Entities;
using Shared.Contracts.Enums;
using Shared.Contracts.Events.Card;
using Shared.Contracts.Models;
using MediatR;
using MassTransit;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CardService.Application.Commands.Cards;

public sealed record CreateCardCommand(
    Guid UserId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    string CardNumber,
    Guid IssuerId,
    decimal CreditLimit,
    decimal OutstandingBalance,
    int BillingCycleStartDay,
    bool IsDefault,
    string AuthorizationHeader) : IRequest<CardResult>;

public record UserDto(Guid Id, string Email, string FullName);

public sealed class CreateCardCommandHandler(
    ICardRepository cardRepository,
    IPublishEndpoint publishEndpoint,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
    : IRequestHandler<CreateCardCommand, CardResult>
{
    public async Task<CardResult> Handle(CreateCardCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty) return new CardResult { Success = false, ErrorCode = "Forbidden", Message = "Not authorized." };

        var digits = CardHelpers.DigitsOnly(request.CardNumber);
        var network = CardHelpers.DetectNetwork(digits);
        if (network == CardNetwork.Unknown) return new CardResult { Success = false, Message = "Unsupported card network." };

        var issuer = await cardRepository.GetIssuerByIdAsync(request.IssuerId, cancellationToken);
        if (issuer is null || issuer.Network != network) return new CardResult { Success = false, Message = "Issuer mismatch or not found." };

        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
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
            CreditLimit = request.CreditLimit,
            OutstandingBalance = request.OutstandingBalance,
            BillingCycleStartDay = request.BillingCycleStartDay,
            IsDefault = request.IsDefault,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (card.IsDefault) await cardRepository.UnsetDefaultForUserAsync(request.UserId, null, cancellationToken);
        await cardRepository.AddAsync(card, cancellationToken);

        // Fetch user details for notification
        var (userSuccess, user, _) = await FetchUserDetailsAsync(request.UserId, request.AuthorizationHeader, cancellationToken);
        if (userSuccess && user is not null)
        {
            await publishEndpoint.Publish<ICardAdded>(new
            {
                CardId = card.Id,
                card.UserId,
                user.Email,
                user.FullName,
                CardNumberLast4 = card.Last4,
                CardHolderName = card.CardholderName,
                AddedAt = card.CreatedAtUtc
            }, cancellationToken);
        }

        return new CardResult { Success = true, Message = "Card created.", Card = CardMapping.ToDto(card) };
    }

    private async Task<(bool Success, UserDto? User, string? Error)> FetchUserDetailsAsync(Guid userId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/";
            client.BaseAddress = new Uri(baseUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/identity/users/{userId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return (false, null, $"Failed to fetch user: {response.ReasonPhrase}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return (result?.Success ?? false, result?.Data, result?.Message);
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }
}
