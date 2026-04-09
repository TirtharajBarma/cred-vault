using System.Net.Http.Json;
using CardService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CardService.Infrastructure.Services;

public class BillingServiceClient : IBillingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BillingServiceClient> _logger;
    private const string BillingServiceBaseUrl = "http://localhost:5003";

    public BillingServiceClient(HttpClient httpClient, ILogger<BillingServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BillingServiceBaseUrl);
        _logger = logger;
    }

    public async Task<bool> HasPendingBillAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/billing/bills/has-pending/{cardId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<HasPendingBillResponse>(cancellationToken: cancellationToken);
                return result?.HasPendingBill ?? false;
            }

            _logger.LogWarning("Failed to check pending bills for card {CardId}. Status: {Status}", cardId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking pending bills for card {CardId}", cardId);
            return false;
        }
    }

    private class HasPendingBillResponse
    {
        public bool HasPendingBill { get; set; }
    }
}
