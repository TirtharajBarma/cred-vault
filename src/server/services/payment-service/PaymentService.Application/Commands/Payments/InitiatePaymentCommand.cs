using System.Net.Http.Headers;
using System.Text.Json;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Interfaces;
using PaymentService.Application.Services;
using Shared.Contracts.Events.Payment;

namespace PaymentService.Application.Commands.Payments;

public record InitiatePaymentCommand(
    Guid UserId,
    Guid CardId,
    Guid BillId,
    decimal Amount,
    PaymentType PaymentType,
    string AuthorizationHeader) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(bool Success, Guid? PaymentId, string? Error, bool OtpRequired = false, bool UserVerified = false);

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
        logger.LogInformation("Payment record created: PaymentId={PaymentId}", payment.Id);

        var (userSuccess, user, _) = await FetchUserDetailsAsync(payment.UserId, request.AuthorizationHeader, cancellationToken);
        
        if (!userSuccess || user == null)
        {
            logger.LogWarning("Payment {PaymentId}: User verification failed - proceeding but flagging", payment.Id);
        }

        var riskScore = RiskCalculator.Calculate(payment.Amount);
        var otpRequired = riskScore >= 50 && riskScore < 75;
        var isBlocked = riskScore >= 75;

        if (isBlocked)
        {
            logger.LogError("Payment {PaymentId} BLOCKED: RiskScore={RiskScore} exceeds threshold", payment.Id, riskScore);
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = "Risk score exceeded threshold";
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new InitiatePaymentResult(false, payment.Id, "Payment blocked due to high risk score");
        }

        if (otpRequired)
        {
            logger.LogInformation("Publishing IPaymentInitiated for PaymentId={PaymentId}, RiskScore={RiskScore}, OTP required",
                payment.Id, riskScore);
            await publishEndpoint.Publish<IPaymentInitiated>(new
            {
                PaymentId   = payment.Id,
                UserId      = payment.UserId,
                Email       = user?.Email ?? string.Empty,
                FullName    = user?.FullName ?? "Unknown",
                CardId      = payment.CardId,
                BillId      = payment.BillId,
                Amount      = payment.Amount,
                PaymentType = payment.PaymentType.ToString(),
                CreatedAt   = payment.CreatedAtUtc,
                RiskScore   = riskScore
            }, cancellationToken);
            return new InitiatePaymentResult(true, payment.Id, null, OtpRequired: true, UserVerified: userSuccess);
        }

        logger.LogInformation("Publishing IPaymentCompleted for PaymentId={PaymentId}, Amount={Amount}, RiskScore={RiskScore}",
            payment.Id, payment.Amount, riskScore);
        await publishEndpoint.Publish<IPaymentCompleted>(new
        {
            PaymentId    = payment.Id,
            UserId       = payment.UserId,
            Email        = user?.Email ?? string.Empty,
            FullName     = user?.FullName ?? "Unknown",
            CardId       = payment.CardId,
            BillId       = payment.BillId,
            Amount       = payment.Amount,
            RiskScore    = riskScore,
            RiskDecision = RiskDecision.AutoApproved.ToString(),
            CompletedAt  = DateTime.UtcNow
        }, cancellationToken);

        return new InitiatePaymentResult(true, payment.Id, null, OtpRequired: false, UserVerified: userSuccess);
    }

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
