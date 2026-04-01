using System.Net.Http.Headers;
using System.Text.Json;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using Shared.Contracts.Events.Payment;
using Shared.Contracts.Events.Saga;

namespace PaymentService.Application.Commands.Payments;

public record InitiatePaymentCommand(
    Guid UserId,
    Guid CardId,
    Guid BillId,
    decimal Amount,
    PaymentType PaymentType,
    string AuthorizationHeader) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(bool Success, Guid? PaymentId, string? Error, bool OtpRequired = true, string? OtpCode = null);

public record UserDto(Guid Id, string Email, string FullName);

public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<InitiatePaymentCommandHandler> logger) : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private record BillDto(Guid UserId, decimal MinDue);

    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("InitiatePayment: UserId={UserId}, BillId={BillId}, Amount={Amount}",
            request.UserId, request.BillId, request.Amount);

        if (request.Amount <= 0)
        {
            logger.LogWarning("Payment rejected: invalid amount {Amount}", request.Amount);
            return new InitiatePaymentResult(false, null, "Amount must be greater than zero");
        }

        var bill = await FetchBillAsync(request.BillId, request.AuthorizationHeader, cancellationToken);
        
        if (bill is null)
        {
            logger.LogWarning("Payment rejected: could not verify bill {BillId}", request.BillId);
            return new InitiatePaymentResult(false, null, "Could not verify bill with Billing Service");
        }

        if (bill.UserId != request.UserId)
        {
            logger.LogWarning("Payment rejected: IDOR attempt - Bill {BillId} belongs to {ActualUserId}, not {RequestUserId}",
                request.BillId, bill.UserId, request.UserId);
            return new InitiatePaymentResult(false, null, "Bill does not belong to the user");
        }

        var otpCode = GenerateOtp();
        var otpExpires = DateTime.UtcNow.AddMinutes(10);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            BillId = request.BillId,
            Amount = request.Amount,
            PaymentType = request.PaymentType,
            Status = PaymentStatus.Initiated,
            OtpCode = otpCode,
            OtpExpiresAtUtc = otpExpires,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        await paymentRepository.AddAsync(payment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Payment record created: PaymentId={PaymentId}", payment.Id);

        var (userSuccess, user, _) = await FetchUserDetailsAsync(payment.UserId, request.AuthorizationHeader, cancellationToken);
        
        if (!userSuccess || user == null)
        {
            logger.LogWarning("Payment {PaymentId}: User verification failed - proceeding but flagging", payment.Id);
        }

        var riskScore = payment.Amount > 10000 ? 85m : payment.Amount > 5000 ? 60m : 20m;

        logger.LogInformation("Starting SAGA orchestration for PaymentId={PaymentId}, Amount={Amount}, RiskScore={RiskScore}, OTP={OtpCode}",
            payment.Id, payment.Amount, riskScore, otpCode);

        await publishEndpoint.Publish<IStartPaymentOrchestration>(new
        {
            CorrelationId = payment.Id,
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Email = user?.Email ?? string.Empty,
            FullName = user?.FullName ?? "Unknown",
            CardId = payment.CardId,
            BillId = payment.BillId,
            Amount = payment.Amount,
            PaymentType = payment.PaymentType.ToString(),
            RiskScore = riskScore,
            OtpCode = otpCode,
            StartedAt = DateTime.UtcNow
        }, cancellationToken);

        await publishEndpoint.Publish<IPaymentOtpGenerated>(new
        {
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Email = user?.Email ?? string.Empty,
            FullName = user?.FullName ?? "Unknown",
            Amount = payment.Amount,
            OtpCode = otpCode,
            ExpiresAtUtc = otpExpires
        }, cancellationToken);

        logger.LogInformation("SAGA orchestration started: PaymentId={PaymentId}, OTP={OtpCode}", payment.Id, otpCode);

        return new InitiatePaymentResult(true, payment.Id, null, OtpRequired: true, OtpCode: otpCode);
    }

    private static string GenerateOtp() =>
        Random.Shared.Next(100000, 999999).ToString();

    private async Task<BillDto?> FetchBillAsync(Guid billId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:BillingService"] ?? "http://localhost:5003";
            client.BaseAddress = new Uri(baseUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/billing/bills/{billId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Billing service returned {Status} for Bill {BillId}", response.StatusCode, billId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<Shared.Contracts.Models.ApiResponse<BillDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return result?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching bill {BillId} from Billing Service", billId);
            return null;
        }
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
            
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity service returned {Status} for User {UserId}", response.StatusCode, userId);
                return (false, null, $"Identity service error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<UserResponseWrapper>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Data?.User == null)
            {
                logger.LogWarning("Identity service returned null user for {UserId}", userId);
                return (false, null, "User not found");
            }
            
            return (true, result.Data.User, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching user {UserId}", userId);
            return (false, null, "External service error");
        }
    }

    private class UserResponseWrapper
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public UserDataWrapper? Data { get; set; }
    }

    private class UserDataWrapper
    {
        public UserDto? User { get; set; }
    }
}
