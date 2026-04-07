using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using PaymentService.Application.Commands.Payments;
using PaymentService.Application.Queries.Payments;
using PaymentService.Domain.Enums;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Authorize]
public class PaymentsController(IMediator mediator) : BaseApiController
{
    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiatePaymentRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

        if (!Enum.TryParse<PaymentType>(request.PaymentType, ignoreCase: true, out var paymentType))
            return BadRequest(BadRequestResponse($"Invalid PaymentType. Valid values: {string.Join(", ", Enum.GetNames<PaymentType>())}"));

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        
        decimal? rewardsAmount = null;
        if (request.RewardsPoints.HasValue && request.RewardsPoints > 0)
        {
            rewardsAmount = request.RewardsPoints.Value * 0.25m; // 1 point = 25 paise (₹0.25)
        }
        
        var command = new InitiatePaymentCommand(userId.Value, request.CardId, request.BillId, request.Amount, paymentType, authHeader, rewardsAmount);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequestResponse(result.Error ?? "Payment initiation failed.");

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = result.RewardsApplied ? "Payment initiated with rewards applied. OTP required." : "Payment initiated. OTP required — call /verify-otp to complete.",
            Data = new
            {
                PaymentId = result.PaymentId,
                OtpRequired = result.OtpRequired,
                Status = "Pending OTP Verification",
                RewardsApplied = result.RewardsApplied,
                RewardsAmount = result.RewardsAmount,
                FinalAmount = result.FinalAmount
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{paymentId:guid}/verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        Guid paymentId,
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

        var command = new VerifyOtpCommand(paymentId, userId.Value, request.OtpCode);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequestResponse(result.Error ?? "OTP verification failed.");

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "OTP verified. Payment is processing.",
            Data = new { PaymentId = paymentId, Status = "Processing" },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("{paymentId:guid}/resend-otp")]
    public async Task<IActionResult> ResendOtp(Guid paymentId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var result = await mediator.Send(new ResendOtpCommand(paymentId, userId.Value, authHeader), cancellationToken);

        if (!result.Success)
            return BadRequestResponse(result.Error ?? "Failed to resend OTP.");

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "OTP resent successfully.",
            Data = new
            {
                PaymentId = paymentId,
                OtpExpiresAtUtc = result.ExpiresAtUtc
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPayments(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

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
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

        var payment = await mediator.Send(new GetPaymentByIdQuery(paymentId, userId.Value), cancellationToken);

        if (payment is null)
            return NotFound(BadRequestResponse("Payment not found."));

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
            return Unauthorized(BadRequestResponse("User identity is missing from token."));

        var transactions = await mediator.Send(new GetPaymentTransactionsQuery(paymentId, userId.Value), cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Transactions fetched.",
            Data = transactions,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

public record InitiatePaymentRequest(Guid CardId, Guid BillId, decimal Amount, string PaymentType, int? RewardsPoints = null);
public record VerifyOtpRequest(string OtpCode);
