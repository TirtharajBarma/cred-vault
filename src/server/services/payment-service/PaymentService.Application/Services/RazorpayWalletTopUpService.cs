using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;

namespace PaymentService.Application.Services;

public interface IRazorpayWalletTopUpService
{
    Task<RazorpayOrderCreateResult> CreateOrderAsync(Guid userId, decimal amount, string description, CancellationToken ct = default);
    Task<RazorpayVerifyResult> VerifyAsync(Guid userId, Guid topUpId, string orderId, string paymentId, string signature, CancellationToken ct = default);
    string? GetPublicKey();
}

public sealed record RazorpayOrderCreateResult(
    bool Success,
    string? Error,
    Guid? TopUpId = null,
    string? OrderId = null,
    long AmountInPaise = 0,
    string Currency = "INR",
    string? KeyId = null,
    string? DisplayName = null,
    string? Description = null);

public sealed record RazorpayVerifyResult(
    bool Success,
    string? Error,
    decimal? NewBalance = null,
    bool AlreadyProcessed = false);

public sealed class RazorpayWalletTopUpService(
    IRazorpayWalletTopUpRepository topUpRepository,
    IWalletService walletService,
    IWalletRepository walletRepository,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<RazorpayWalletTopUpService> logger) : IRazorpayWalletTopUpService
{
    private const string RazorpayOrdersUrl = "https://api.razorpay.com/v1/orders";

    public string? GetPublicKey()
    {
        var keyId = (configuration["Razorpay:KeyId"] ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(keyId) ? null : keyId;
    }

    public async Task<RazorpayOrderCreateResult> CreateOrderAsync(Guid userId, decimal amount, string description, CancellationToken ct = default)
    {
        if (amount <= 0)
            return new RazorpayOrderCreateResult(false, "Amount must be greater than zero.");

        var keyId = GetPublicKey();
        var keySecret = (configuration["Razorpay:KeySecret"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
            return new RazorpayOrderCreateResult(false, "Razorpay is not configured on the server.");

        var topUpId = Guid.NewGuid();
        var amountInPaise = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        var effectiveDescription = string.IsNullOrWhiteSpace(description) ? "Wallet top-up" : description.Trim();

        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, RazorpayOrdersUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BuildBasicAuth(keyId, keySecret));
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                amount = amountInPaise,
                currency = "INR",
                receipt = topUpId.ToString("N"),
                notes = new
                {
                    topUpId,
                    userId,
                    description = effectiveDescription
                }
            }),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await client.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Razorpay order creation failed with status {Status}: {Payload}", response.StatusCode, payload);
                return new RazorpayOrderCreateResult(false, "Unable to create Razorpay order.");
            }

            using var doc = JsonDocument.Parse(payload);
            var orderId = doc.RootElement.GetProperty("id").GetString();
            if (string.IsNullOrWhiteSpace(orderId))
                return new RazorpayOrderCreateResult(false, "Razorpay order id missing in response.");

            var topUp = new RazorpayWalletTopUp
            {
                Id = topUpId,
                UserId = userId,
                Amount = amount,
                Description = effectiveDescription,
                RazorpayOrderId = orderId,
                Status = RazorpayWalletTopUpStatus.Created,
                CreatedAtUtc = DateTime.UtcNow
            };

            await topUpRepository.AddAsync(topUp);

            return new RazorpayOrderCreateResult(
                true,
                null,
                TopUpId: topUpId,
                OrderId: orderId,
                AmountInPaise: amountInPaise,
                Currency: "INR",
                KeyId: keyId,
                DisplayName: configuration["Razorpay:DisplayName"] ?? "CredVault",
                Description: effectiveDescription);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create Razorpay order for wallet top-up. UserId={UserId}, Amount={Amount}", userId, amount);
            return new RazorpayOrderCreateResult(false, "Unable to create Razorpay order.");
        }
    }

    public async Task<RazorpayVerifyResult> VerifyAsync(Guid userId, Guid topUpId, string orderId, string paymentId, string signature, CancellationToken ct = default)
    {
        var keySecret = (configuration["Razorpay:KeySecret"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(keySecret))
            return new RazorpayVerifyResult(false, "Razorpay is not configured on the server.");

        var topUp = await topUpRepository.GetByIdAsync(topUpId);
        if (topUp is null || topUp.UserId != userId)
            return new RazorpayVerifyResult(false, "Wallet top-up request not found.");

        if (topUp.Status == RazorpayWalletTopUpStatus.Verified)
        {
            var wallet = await walletService.GetWalletAsync(userId, ct);
            return new RazorpayVerifyResult(true, null, wallet?.Balance ?? 0m, AlreadyProcessed: true);
        }

        var existingCredit = await walletRepository.GetTransactionByRelatedPaymentIdAsync(topUpId);
        if (existingCredit is not null)
        {
            topUp.RazorpayPaymentId = paymentId;
            topUp.RazorpaySignature = signature;
            topUp.Status = RazorpayWalletTopUpStatus.Verified;
            topUp.VerifiedAtUtc ??= DateTime.UtcNow;
            topUp.FailureReason = null;
            await topUpRepository.UpdateAsync(topUp);
            return new RazorpayVerifyResult(true, null, existingCredit.BalanceAfter, AlreadyProcessed: true);
        }

        if (!string.Equals(topUp.RazorpayOrderId, orderId, StringComparison.Ordinal))
            return new RazorpayVerifyResult(false, "Razorpay order mismatch.");

        if (!IsValidSignature(orderId, paymentId, signature, keySecret))
        {
            topUp.Status = RazorpayWalletTopUpStatus.Failed;
            topUp.FailureReason = "Invalid Razorpay signature.";
            topUp.RazorpayPaymentId = paymentId;
            topUp.RazorpaySignature = signature;
            await topUpRepository.UpdateAsync(topUp);
            return new RazorpayVerifyResult(false, "Payment verification failed.");
        }

        var newBalance = await walletService.TopUpAsync(
            userId,
            topUp.Amount,
            $"Wallet top-up via Razorpay ({paymentId})",
            topUp.Id,
            ct);

        topUp.RazorpayPaymentId = paymentId;
        topUp.RazorpaySignature = signature;
        topUp.Status = RazorpayWalletTopUpStatus.Verified;
        topUp.VerifiedAtUtc = DateTime.UtcNow;
        topUp.FailureReason = null;
        await topUpRepository.UpdateAsync(topUp);

        return new RazorpayVerifyResult(true, null, newBalance);
    }

    private static string BuildBasicAuth(string keyId, string keySecret)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
    }

    private static bool IsValidSignature(string orderId, string paymentId, string signature, string keySecret)
    {
        var payload = $"{orderId}|{paymentId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var generated = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(generated),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }
}
