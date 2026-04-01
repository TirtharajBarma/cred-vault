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

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = "Payment initiated. OTP required — call /verify-otp to complete.",
            Data = new
            {
                PaymentId = result.PaymentId,
                OtpRequired = result.OtpRequired,
                Status = "Pending OTP Verification",
                DevOtp = result.OtpCode
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
            return Unauthorized(Fail("User identity is missing from token."));

        var command = new VerifyOtpCommand(paymentId, userId.Value, request.OtpCode);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
            return BadRequest(Fail(result.Error ?? "OTP verification failed."));

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "OTP verified. Payment is processing.",
            Data = new { PaymentId = paymentId, Status = "Processing" },
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
}

public record InitiatePaymentRequest(Guid CardId, Guid BillId, decimal Amount, string PaymentType);
public record VerifyOtpRequest(string OtpCode);
