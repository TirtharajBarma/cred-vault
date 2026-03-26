using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Shared.Contracts.Models;

namespace BillingService.API.Services;

public sealed class CardServiceClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CardServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public sealed class CardSummaryDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid IssuerId { get; set; }
        public string IssuerName { get; set; } = string.Empty;
        public string Network { get; set; } = string.Empty;
        public string CardholderName { get; set; } = string.Empty;
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public string Last4 { get; set; } = string.Empty;
        public string MaskedNumber { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public decimal OutstandingBalance { get; set; }
        public int BillingCycleStartDay { get; set; }
        public bool IsDefault { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    // Used by admin bill generation — bypasses user ownership check
    public async Task<(bool success, CardSummaryDto? card, string? errorMessage, HttpStatusCode? statusCode)> GetCardByIdForAdminAsync(
        Guid cardId,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/cards/admin/{cardId}");

        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
        }

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (true, null, null, response.StatusCode);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
        {
            return (false, null, "Unauthorized to fetch card from CardService.", response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (false, null, $"CardService returned {(int)response.StatusCode}.", response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = JsonSerializer.Deserialize<ApiResponse<CardSummaryDto>>(json, JsonOptions);

        if (parsed is null)
        {
            return (false, null, "CardService returned an unreadable response.", response.StatusCode);
        }

        if (!parsed.Success)
        {
            return (false, null, parsed.Message, response.StatusCode);
        }

        return (true, parsed.Data, null, response.StatusCode);
    }

    public sealed class AddTransactionRequest
    {
        public int Type { get; set; } // 1=Purchase, 2=Payment, 3=Refund
        public decimal Amount { get; set; }
        public string? Description { get; set; }
    }

    public async Task<(bool success, string? errorMessage, HttpStatusCode? statusCode)> AddTransactionAsync(
        Guid cardId,
        AddTransactionRequest body,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/cards/{cardId}/transactions");

        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorizationHeader);
        }

        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), System.Text.Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            try 
            {
                var parsed = JsonSerializer.Deserialize<ApiResponse<object>>(json, JsonOptions);
                return (false, parsed?.Message ?? $"CardService returned {(int)response.StatusCode}.", response.StatusCode);
            }
            catch 
            {
                return (false, $"CardService returned {(int)response.StatusCode}.", response.StatusCode);
            }
        }

        return (true, null, response.StatusCode);
    }
}
