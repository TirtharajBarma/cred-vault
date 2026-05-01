using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Services;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/wallets/razorpay")]
[Authorize]
public class RazorpayWalletController(IRazorpayWalletTopUpService razorpayService) : BaseApiController
{
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder(
        [FromBody] RazorpayCreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        if (request.Amount <= 0)
            return BadRequestResponse("Amount must be greater than zero.");

        var result = await razorpayService.CreateOrderAsync(
            userId.Value,
            request.Amount,
            request.Description ?? "Wallet top-up",
            cancellationToken);

        if (!result.Success)
            return BadRequestResponse(result.Error);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Razorpay order created.",
            Data = new
            {
                result.TopUpId,
                result.OrderId,
                result.AmountInPaise,
                result.Currency,
                result.KeyId,
                result.DisplayName,
                result.Description
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("verify")]
    public async Task<IActionResult> Verify(
        [FromBody] RazorpayVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        var result = await razorpayService.VerifyAsync(
            userId.Value,
            request.TopUpId,
            request.OrderId,
            request.PaymentId,
            request.Signature,
            cancellationToken);

        if (!result.Success)
            return BadRequestResponse(result.Error);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Payment verified successfully.",
            Data = new
            {
                result.NewBalance,
                result.AlreadyProcessed
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("public-key")]
    public IActionResult GetPublicKey()
    {
        var keyId = razorpayService.GetPublicKey();
        if (string.IsNullOrWhiteSpace(keyId))
            return BadRequestResponse("Razorpay is not configured.");

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Data = new { KeyId = keyId },
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

public record RazorpayCreateOrderRequest(decimal Amount, string? Description = null);
public record RazorpayVerifyRequest(
    Guid TopUpId,
    string OrderId,
    string PaymentId,
    string Signature);
