using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.Commands.Payments;
using PaymentService.Application.Queries.Payments;
using PaymentService.Domain.Enums;
using PaymentService.Infrastructure.Persistence.Sql;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;
namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController(IMediator mediator, IWebHostEnvironment env) : BaseApiController
{
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        if (!Enum.TryParse<PaymentType>(request.PaymentType, ignoreCase: true, out var paymentType))
            return BadRequest(Fail($"Invalid PaymentType. Valid values: {string.Join(", ", Enum.GetNames<PaymentType>())}"));

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var command = new InitiatePaymentCommand(userId.Value, request.CardId, request.BillId, request.Amount, paymentType, authHeader);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequest(Fail(result.Error ?? "Payment initiation failed."));

        // In Development, return the OTP directly so you can test without email/SMS.
        // There is an inherent race condition here: the HTTP request publishes 'PaymentInitiated' 
        // to RabbitMQ and returns. MassTransit processes it asynchronously, meaning the Saga 
        // might not have generated and saved the OTP into the database *yet* when this line runs.
        // We poll briefly to wait for the saga to complete its first step.
        string? devOtp = null;
        if (result.OtpRequired && env.IsDevelopment())
        {
            var db = HttpContext.RequestServices.GetRequiredService<PaymentDbContext>();
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(200);
                var saga = await db.PaymentSagas
                    .AsNoTracking()
                    .Where(x => x.PaymentId == result.PaymentId && x.OtpCode != null)
                    .Select(x => new { x.OtpCode })
                    .FirstOrDefaultAsync();
                if (saga?.OtpCode is not null)
                {
                    devOtp = saga.OtpCode;
                    break;
                }
            }
        }

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = result.OtpRequired
                ? "Payment initiated. OTP required — call /verify-otp to complete."
                : "Payment initiated and processing.",
            Data = new
            {
                PaymentId   = result.PaymentId,
                OtpRequired = result.OtpRequired,
                // Only present in Development — remove in production
                DevOtp      = devOtp
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{paymentId:guid}/verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        Guid paymentId,
        [FromBody] VerifyOtpRequest request,
        [FromServices] PaymentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var saga = await dbContext.PaymentSagas
            .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken);

        if (saga is null)
            return NotFound(Fail("Payment not found."));

        if (saga.CurrentState != "RiskCheckPassed")
            return BadRequest(Fail("Payment is not awaiting OTP verification."));

        if (string.IsNullOrWhiteSpace(saga.OtpCode))
            return BadRequest(Fail("No OTP was generated for this payment."));

        if (saga.OtpExpiresAtUtc.HasValue && saga.OtpExpiresAtUtc.Value < DateTime.UtcNow)
            return BadRequest(Fail("OTP has expired. Please initiate a new payment."));

        if (!string.Equals(saga.OtpCode, request.OtpCode.Trim(), StringComparison.Ordinal))
            return BadRequest(Fail("Invalid OTP."));

        // Clear OTP after successful use (one-time use)
        saga.OtpCode = null;
        saga.OtpExpiresAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);

        var command = new VerifyPaymentOtpCommand(paymentId, request.OtpCode);
        await mediator.Send(command, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "OTP verified. Payment is processing.",
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{paymentId:guid}/reverse")]
    public async Task<IActionResult> Reverse(Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        var command = new ReversePaymentCommand(paymentId, userId.Value);
        var result = await mediator.Send(command, cancellationToken);

        if (!result)
            return NotFound(Fail("Payment not found or not authorized."));

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Payment reversed.",
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPayments(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        var payments = await mediator.Send(new GetAllPaymentsQuery(userId.Value), cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Payments fetched.",
            Data = payments,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<IActionResult> GetById(Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        var payment = await mediator.Send(new GetPaymentByIdQuery(paymentId, userId.Value), cancellationToken);

        if (payment is null)
            return NotFound(Fail("Payment not found."));

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Payment fetched.",
            Data = payment,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("{paymentId:guid}/transactions")]
    public async Task<IActionResult> GetTransactions(Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        var transactions = await mediator.Send(new GetPaymentTransactionsQuery(paymentId, userId.Value), cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Transactions fetched.",
            Data = transactions,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("{paymentId:guid}/risk")]
    public async Task<IActionResult> GetRiskScore(Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(Fail("User identity is missing from token."));

        var risk = await mediator.Send(new GetRiskScoreQuery(paymentId, userId.Value), cancellationToken);

        if (risk is null)
            return NotFound(Fail("Risk score not found."));

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Risk score fetched.",
            Data = risk,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

public record InitiatePaymentRequest(Guid CardId, Guid BillId, decimal Amount, string PaymentType);
public record VerifyOtpRequest(string OtpCode);
