using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
    string AuthorizationHeader,
    decimal? RewardsAmount = null) : IRequest<InitiatePaymentResult>;

public record InitiatePaymentResult(bool Success, Guid? PaymentId, string? Error, bool OtpRequired = true, bool RewardsApplied = false, decimal? RewardsAmount = null, decimal? FinalAmount = null);

public record UserDto(Guid Id, string Email, string FullName);

public class InitiatePaymentCommandHandler(
    IPaymentRepository paymentRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ISendEndpointProvider sendEndpointProvider,
    IHttpClientFactory httpClientFactory,           // call other services
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<InitiatePaymentCommandHandler> logger) : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private record BillDto(Guid UserId, Guid CardId, decimal Amount, decimal MinDue, decimal? AmountPaid);

    public async Task<InitiatePaymentResult> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("InitiatePayment: UserId={UserId}, BillId={BillId}, Amount={Amount}, RewardsAmount={RewardsAmount}",
            request.UserId, request.BillId, request.Amount, request.RewardsAmount);

        // Cleanup stuck payments for this user/bill before creating new one
        //! if user started payment before and didn't finish -> make them fail
        await MarkStuckPaymentsFailedAsync(request.UserId, request.BillId, cancellationToken);

        if (request.Amount <= 0)
        {
            logger.LogWarning("Payment rejected: invalid amount {Amount}", request.Amount);
            return new InitiatePaymentResult(false, null, "Amount must be greater than zero");
        }

        // fetch the bill
        var billResult = await FetchBillAsync(request.BillId, request.AuthorizationHeader, cancellationToken);
        
        if (billResult.Bill is null)
        {
            logger.LogWarning("Payment rejected: could not verify bill {BillId} - {Error}", request.BillId, billResult.Error);
            return new InitiatePaymentResult(false, null, billResult.Error ?? "Could not verify bill with Billing Service");
        }

        var bill = billResult.Bill;

        if (bill.UserId != request.UserId)
        {
            logger.LogWarning("Payment rejected: IDOR attempt - Bill {BillId} belongs to {ActualUserId}, not {RequestUserId}",
                request.BillId, bill.UserId, request.UserId);
            return new InitiatePaymentResult(false, null, "Bill does not belong to the user");
        }

        if (bill.CardId != request.CardId)
        {
            logger.LogWarning("Payment rejected: Card mismatch - Bill {BillId} is for Card {BillCardId}, but payment uses Card {RequestCardId}",
                request.BillId, bill.CardId, request.CardId);
            return new InitiatePaymentResult(false, null, "Selected card does not match the bill's card");
        }

        var existingPaid = bill.AmountPaid ?? 0m;
        var outstandingAmount = Math.Max(0m, bill.Amount - existingPaid);

        if (outstandingAmount <= 0)
        {
            logger.LogWarning("Payment rejected: Bill {BillId} already settled", request.BillId);
            return new InitiatePaymentResult(false, null, "Bill is already settled");
        }

        // Calculate rewards amount for display but DON'T redeem yet
        //! Rewards will be redeemed AFTER OTP verification in the SAGA
        decimal rewardsApplied = 0;
        if (request.RewardsAmount.HasValue && request.RewardsAmount > 0)
        {
            // you can't apply more rewards than the bill amt.
            rewardsApplied = Math.Min(request.RewardsAmount.Value, outstandingAmount);
            logger.LogInformation("Rewards will be applied after payment: {RewardsAmount}", rewardsApplied);
        }

        // Check payment amount against outstanding (after potential rewards)
        var finalAmount = request.Amount;
        
        if (finalAmount > outstandingAmount)
        {
            logger.LogWarning("Payment rejected: Amount {Amount} exceeds outstanding {Outstanding} for Bill {BillId}",
                finalAmount, outstandingAmount, request.BillId);
            return new InitiatePaymentResult(false, null, $"Payment exceeds outstanding balance. Outstanding: {outstandingAmount:0.00}");
        }

        // full payment
        if (request.PaymentType == PaymentType.Full && Math.Abs(finalAmount - outstandingAmount) > 0.01m)
        {
            logger.LogWarning("Payment rejected: Full payment amount {Amount} does not match outstanding {Outstanding} for Bill {BillId}",
                finalAmount, outstandingAmount, request.BillId);
            return new InitiatePaymentResult(false, null, $"Full payment must equal outstanding balance ({outstandingAmount:0.00})");
        }

        var otpCode = GenerateOtp();
        var otpExpires = DateTime.UtcNow.AddMinutes(10);

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            BillId = request.BillId,
            Amount = finalAmount,
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

        logger.LogInformation("Starting SAGA orchestration for PaymentId={PaymentId}, Amount={Amount}, OTP={OtpCode}",
            payment.Id, payment.Amount, otpCode);

        // Pass rewards info to SAGA - it will redeem AFTER successful payment
        await publishEndpoint.Publish<IStartPaymentOrchestration>(new
        {
            CorrelationId = payment.Id,     // ← You generate it when payment starts
            PaymentId = payment.Id,
            UserId = payment.UserId,
            Email = user?.Email ?? string.Empty,
            FullName = user?.FullName ?? "Unknown",
            CardId = payment.CardId,
            BillId = payment.BillId,
            Amount = payment.Amount,
            PaymentType = payment.PaymentType.ToString(),
            OtpCode = otpCode,
            RewardsAmount = rewardsApplied,  // Pass to SAGA
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

        return new InitiatePaymentResult(
            true, 
            payment.Id, 
            null, 
            OtpRequired: true,
            RewardsApplied: rewardsApplied > 0,
            RewardsAmount: rewardsApplied > 0 ? rewardsApplied : null,
            FinalAmount: finalAmount);
    }

    private static string GenerateOtp()
    {
        return RandomNumberGenerator.GetInt32(100000, 999999).ToString();
    }

    private record FetchBillResult(BillDto? Bill, string? Error, int StatusCode);

    private async Task<FetchBillResult> FetchBillAsync(Guid billId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:BillingService"] ?? "http://localhost:5003";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);

            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/billing/bills/{billId}");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            
            var content = await response.Content.ReadAsStringAsync(ct);     // convert req to string

            switch (response.StatusCode)
            {
                case System.Net.HttpStatusCode.NotFound:
                    logger.LogWarning("Bill {BillId} not found in Billing Service", billId);
                    return new FetchBillResult(null, "Bill not found", 404);

                case System.Net.HttpStatusCode.Forbidden:
                case System.Net.HttpStatusCode.Unauthorized:
                    logger.LogWarning("Unauthorized access to bill {BillId}", billId);
                    return new FetchBillResult(null, "Unauthorized access to bill", 403);

                case System.Net.HttpStatusCode.BadRequest:
                    logger.LogWarning("Bad request for bill {BillId}: {Content}", billId, content);
                    return new FetchBillResult(null, "Invalid bill data", 400);

                case System.Net.HttpStatusCode.InternalServerError:
                case System.Net.HttpStatusCode.ServiceUnavailable:
                    logger.LogError("Billing service error {Status} for Bill {BillId}: {Content}", response.StatusCode, billId, content);
                    return new FetchBillResult(null, "Billing service temporarily unavailable", (int)response.StatusCode);

                default:
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Billing service returned {Status} for Bill {BillId}", response.StatusCode, billId);
                        return new FetchBillResult(null, $"Billing service error: {response.StatusCode}", (int)response.StatusCode);
                    }
                    break;
            }

            var result = JsonSerializer.Deserialize<Shared.Contracts.Models.ApiResponse<BillDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Data == null)
            {
                logger.LogWarning("Bill {BillId} returned null data from Billing Service", billId);
                return new FetchBillResult(null, "Bill data not available", 0);
            }
            
            return new FetchBillResult(result.Data, null, 200);
        }
        catch (TaskCanceledException)
        {
            logger.LogError("Timeout fetching bill {BillId} from Billing Service", billId);
            return new FetchBillResult(null, "Billing service request timed out", 408);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error fetching bill {BillId} from Billing Service", billId);
            return new FetchBillResult(null, "Unable to connect to Billing Service", 503);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching bill {BillId} from Billing Service", billId);
            return new FetchBillResult(null, "Failed to verify bill", 500);
        }
    }

    private async Task<(bool Success, UserDto? User, string? Error)> FetchUserDetailsAsync(Guid userId, string authHeader, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var baseUrl = configuration["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/";
            client.BaseAddress = new Uri(baseUrl);

            // Use /me endpoint which works with any user token (no admin role required)
            var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/identity/users/me");
            if (!string.IsNullOrEmpty(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Identity service returned {Status} for User /me", response.StatusCode);
                return (false, null, $"Identity service error: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<UserResponseWrapper>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result?.Data?.User == null)
            {
                logger.LogWarning("Identity service returned null user for /me endpoint");
                return (false, null, "User not found");
            }
            
            return (true, result.Data.User, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception fetching user /me");
            return (false, null, "External service error");
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

    private async Task MarkStuckPaymentsFailedAsync(Guid userId, Guid billId, CancellationToken ct)
    {
        try
        {
            var stuckPayments = await paymentRepository.GetStuckPaymentsAsync(userId, billId, ct);
            foreach (var payment in stuckPayments)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "User initiated new payment before completing this one";
                payment.OtpCode = null;
                payment.OtpExpiresAtUtc = null;
                payment.UpdatedAtUtc = DateTime.UtcNow;
                await paymentRepository.UpdateAsync(payment);
                logger.LogInformation("Marked stuck payment as Failed: PaymentId={PaymentId}", payment.Id);

                // direct queue send
                var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:payment-orchestration"));
                await endpoint.Send<IOtpFailed>(new
                {
                    CorrelationId = payment.Id,
                    PaymentId = payment.Id,
                    Reason = "Payment superseded by new payment request",
                    FailedAt = DateTime.UtcNow
                }, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup stuck payments for UserId={UserId}, BillId={BillId}", userId, billId);
        }
    }
}
