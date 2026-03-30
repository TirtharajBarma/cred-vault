using System.Net.Http.Headers;
using System.Text.Json;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Domain.Entities;
using MediatR;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;
using Shared.Contracts.Events.Billing;
using MassTransit;

namespace BillingService.Application.Commands.Bills;

public record GenerateAdminBillCommand(Guid AdminUserId, Guid UserId, Guid CardId, string Currency, string AuthorizationHeader) : IRequest<ApiResponse<Bill>>;

public class GenerateAdminBillCommandHandler(
    IBillRepository billRepository,
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint)
    : IRequestHandler<GenerateAdminBillCommand, ApiResponse<Bill>>
{
    private record CardDto(Guid Id, Guid UserId, string Network, Guid IssuerId, decimal CreditLimit, decimal OutstandingBalance);
    private record UserDto(Guid Id, string Email, string FullName);

    public async Task<ApiResponse<Bill>> Handle(GenerateAdminBillCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.CardId == Guid.Empty)
        {
            return new ApiResponse<Bill> { Success = false, Message = "UserId and CardId are required." };
        }

        var (cardSuccess, card, cardError) = await FetchCardDetailsAsync(request.CardId, request.AuthorizationHeader, cancellationToken);

        if (!cardSuccess || card is null)
        {
            return new ApiResponse<Bill> { Success = false, Message = cardError ?? "Card not found." };
        }

        if (!Enum.TryParse<CardNetwork>(card.Network, true, out var network) || network == CardNetwork.Unknown)
        {
            return new ApiResponse<Bill> { Success = false, Message = "Card network is invalid." };
        }

        if (card.OutstandingBalance <= 0)
        {
            return new ApiResponse<Bill> { Success = false, Message = "Outstanding balance is 0. No bill to generate." };
        }

        var minDue = Math.Max(Math.Round(card.OutstandingBalance * 0.10m, 2, MidpointRounding.AwayFromZero), 10.00m);
        var now = DateTime.UtcNow;

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            CardNetwork = network,
            IssuerId = card.IssuerId,
            Amount = card.OutstandingBalance,
            MinDue = minDue,
            Currency = request.Currency,
            BillingDateUtc = now,
            DueDateUtc = now.AddDays(20),
            Status = BillStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await billRepository.AddAsync(bill, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch User Details for Notification
        var (userSuccess, user, userError) = await FetchUserDetailsAsync(request.UserId, request.AuthorizationHeader, cancellationToken);

        if (userSuccess && user is not null && !string.IsNullOrWhiteSpace(user.Email))
        {
            await publishEndpoint.Publish<IBillGenerated>(new
            {
                BillId = bill.Id,
                bill.UserId,
                user.Email,
                user.FullName,
                bill.CardId,
                bill.Amount,
                DueDate = bill.DueDateUtc,
                GeneratedAt = bill.CreatedAtUtc
            }, cancellationToken);
        }
        else
        {
            Console.WriteLine($"[BillGenerator] Skipping notification - user fetch failed or email missing. UserId: {request.UserId}, Success: {userSuccess}, Error: {userError}");
        }
        
        return new ApiResponse<Bill> { Success = true, Message = "Bill generated.", Data = bill };
    }

    private async Task<(bool Success, CardDto? Card, string? Error)> FetchCardDetailsAsync(Guid cardId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:CardService:BaseUrl"] ?? "http://localhost:5002/";
            client.BaseAddress = new Uri(baseUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/admin/{cardId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return (false, null, $"Failed to fetch card: {response.ReasonPhrase}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ApiResponse<CardDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return (result?.Success ?? false, result?.Data, result?.Message);
        }
        catch (Exception ex) { return (false, null, ex.Message); }
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
