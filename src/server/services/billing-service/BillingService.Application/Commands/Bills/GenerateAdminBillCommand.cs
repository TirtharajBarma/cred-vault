using System.Net.Http.Headers;
using System.Text.Json;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using Shared.Contracts.Events.Billing;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BillingService.Application.Commands.Bills;

public record GenerateAdminBillCommand(Guid AdminUserId, Guid UserId, Guid CardId, string Currency, string AuthorizationHeader) : IRequest<ApiResponse<Bill>>;

public class GenerateAdminBillCommandHandler(IBillRepository bills, IHttpClientFactory http, Microsoft.Extensions.Configuration.IConfiguration config, IUnitOfWork uow, IPublishEndpoint publisher, ILogger<GenerateAdminBillCommandHandler> logger) : IRequestHandler<GenerateAdminBillCommand, ApiResponse<Bill>>
{
    public async Task<ApiResponse<Bill>> Handle(GenerateAdminBillCommand request, CancellationToken ct)
    {
        logger.LogInformation("GenerateAdminBill requested: UserId={UserId}, CardId={CardId}", request.UserId, request.CardId);

        if (request.UserId == Guid.Empty || request.CardId == Guid.Empty)
        {
            logger.LogWarning("GenerateBill rejected: UserId or CardId is empty");
            return new() { Success = false, Message = "UserId and CardId required" };
        }

        var (ok, card, err) = await GetCardAsync(request.CardId, request.AuthorizationHeader, ct);
        if (!ok || card == null)
        {
            logger.LogWarning("Failed to fetch card {CardId}: {Error}", request.CardId, err);
            return new() { Success = false, Message = "Card not found" };
        }

        if (card.UserId != request.UserId)
        {
            logger.LogWarning("IDOR attempt: Card {CardId} belongs to {ActualUserId}, not {RequestUserId}",
                request.CardId, card.UserId, request.UserId);
            return new() { Success = false, Message = "Card does not belong to user" };
        }

        if (!Enum.TryParse<CardNetwork>(card.Network, true, out var network) || network == CardNetwork.Unknown)
        {
            logger.LogWarning("Invalid card network for {CardId}: {Network}", request.CardId, card.Network);
            return new() { Success = false, Message = "Invalid card network" };
        }

        if (card.OutstandingBalance <= 0)
        {
            logger.LogInformation("No balance to bill for CardId={CardId}", request.CardId);
            return new() { Success = false, Message = "No balance to bill" };
        }

        var minDue = Math.Max(Math.Round(card.OutstandingBalance * 0.10m, 2, MidpointRounding.AwayFromZero), 10.00m);
        var now = DateTime.UtcNow;

        var bill = new Bill
        {
            Id = Guid.NewGuid(), UserId = request.UserId, CardId = request.CardId,
            CardNetwork = network, IssuerId = card.IssuerId, Amount = card.OutstandingBalance,
            MinDue = minDue, Currency = request.Currency, BillingDateUtc = now,
            DueDateUtc = now.AddDays(20), Status = BillStatus.Pending, CreatedAtUtc = now, UpdatedAtUtc = now
        };

        await bills.AddAsync(bill, ct);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Bill created: {BillId}, Amount={Amount}, DueDate={DueDate}", bill.Id, bill.Amount, bill.DueDateUtc);

        var (userOk, user, _) = await GetUserAsync(request.UserId, request.AuthorizationHeader, ct);
        if (userOk && user != null && !string.IsNullOrWhiteSpace(user.Email))
        {
            await publisher.Publish(new { BillId = bill.Id, bill.UserId, user.Email, user.FullName, bill.CardId, bill.Amount, DueDate = bill.DueDateUtc, GeneratedAt = bill.CreatedAtUtc }, ct);
            logger.LogInformation("Published IBillGenerated for {BillId}", bill.Id);
        }
        else
        {
            logger.LogWarning("Could not fetch user details for {UserId}: {Error}", request.UserId, userOk ? "null user" : "fetch failed");
        }

        return new() { Success = true, Message = "Bill generated", Data = bill };
    }

    private async Task<(bool, CardDto?, string?)> GetCardAsync(Guid cardId, string auth, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();
            client.BaseAddress = new Uri(config["Services:CardService:BaseUrl"] ?? "http://localhost:5002/");
            var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/admin/{cardId}");
            if (!string.IsNullOrEmpty(auth)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Card service returned {Status} for {CardId}", resp.StatusCode, cardId);
                return (false, null, $"Card service error: {resp.StatusCode}");
            }
            var content = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponse<CardDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Data == null)
            {
                logger.LogWarning("Card service returned null data for {CardId}", cardId);
                return (false, null, "Card not found");
            }
            return (true, result.Data, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching card {CardId}", cardId);
            return (false, null, "External service error");
        }
    }

    private async Task<(bool, UserDto?, string?)> GetUserAsync(Guid userId, string auth, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient();
            client.BaseAddress = new Uri(config["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/");
            var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/identity/users/{userId}");
            if (!string.IsNullOrEmpty(auth)) req.Headers.Authorization = AuthenticationHeaderValue.Parse(auth);
            var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return (false, null, $"User service error: {resp.StatusCode}");
            var content = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponse<UserDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return (result?.Success ?? false, result?.Data, result?.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching user {UserId}", userId);
            return (false, null, "External service error");
        }
    }

    private record CardDto(Guid Id, Guid UserId, string Network, Guid IssuerId, decimal CreditLimit, decimal OutstandingBalance);
    private record UserDto(Guid Id, string Email, string FullName);
}
