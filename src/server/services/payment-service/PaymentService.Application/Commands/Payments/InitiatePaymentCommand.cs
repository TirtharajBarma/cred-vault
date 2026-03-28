using System.Net.Http.Headers;
using System.Text.Json;
using MassTransit;
using MediatR;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Commands.Payments;

public record InitiatePaymentCommand(
    Guid UserId,
    Guid CardId,
    Guid BillId,
    decimal Amount,
    PaymentType PaymentType,
    string AuthorizationHeader) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(bool Success, Guid? PaymentId, string? Error, bool OtpRequired = false);

public record UserDto(Guid Id, string Email, string FullName);

public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IHttpClientFactory httpClientFactory) : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private record BillDto(Guid UserId, decimal MinDue);

    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        // 1. Secure Cross-Service Validation (FETCH FROM BILLING SERVICE)
        var bill = await FetchBillAsync(request.BillId, request.AuthorizationHeader, cancellationToken);
        
        if (bill is null)
        {
            return new InitiatePaymentResult(false, null, "Could not verify bill with Billing Service.");
        }

        if (bill.UserId != request.UserId)
        {
            return new InitiatePaymentResult(false, null, "Bill does not belong to the user.");
        }

        // 2. Simplified Initiation (Risk logic moved to Saga to keep handler simple and linear)
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            BillId = request.BillId,
            Amount = request.Amount,
            PaymentType = request.PaymentType,
            Status = PaymentStatus.Initiated,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await paymentRepository.AddAsync(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // 3. Kick off the Saga (with user details for notifications)
        var (userSuccess, user, _) = await FetchUserDetailsAsync(payment.UserId, request.AuthorizationHeader, cancellationToken);

        await publishEndpoint.Publish<IPaymentInitiated>(new
        {
            PaymentId   = payment.Id,
            UserId      = payment.UserId,
            Email       = user?.Email ?? string.Empty,
            FullName    = user?.FullName ?? "User",
            CardId      = payment.CardId,
            BillId      = payment.BillId,
            Amount      = payment.Amount,
            PaymentType = payment.PaymentType.ToString(),
            CreatedAt   = payment.CreatedAtUtc
        }, cancellationToken);

        // Pre-calculate risk to inform the client immediately if OTP is required.
        // This mirrors the logic in the Saga (RiskScore >= 50 && < 75 means OTP).
        var riskScore = PaymentService.Application.Services.RiskCalculator.Calculate(request.Amount);
        bool otpRequired = riskScore >= 50 && riskScore < 75;

        return new InitiatePaymentResult(true, payment.Id, null, OtpRequired: otpRequired);
    }

    private async Task<BillDto?> FetchBillAsync(Guid billId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("BillingServiceClient");
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/billing/bills/{billId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<Shared.Contracts.Models.ApiResponse<BillDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result?.Data;
        }
        catch { return null; }
    }

    private async Task<(bool Success, UserDto? User, string? Error)> FetchUserDetailsAsync(Guid userId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("IdentityServiceClient");
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/identity/users/{userId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return (false, null, $"Failed to fetch user: {response.ReasonPhrase}");

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<Shared.Contracts.Models.ApiResponse<UserDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return (result?.Success ?? false, result?.Data, result?.Message);
        }
        catch (Exception ex) { return (false, null, ex.Message); }
    }
}
