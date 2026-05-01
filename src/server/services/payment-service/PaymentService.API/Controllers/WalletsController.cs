using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Application.Services;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Authorize]
public class WalletsController(IWalletService walletService) : BaseApiController
{
    [HttpGet("me")]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        var wallet = await walletService.GetWalletAsync(userId.Value, cancellationToken);

        if (wallet is null)
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "No wallet found. Wallet will be created on first top-up.",
                Data = new { HasWallet = false, Balance = 0m },
                TraceId = HttpContext.TraceIdentifier
            });

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Wallet fetched.",
            Data = new
            {
                HasWallet = true,
                WalletId = wallet.Id,
                Balance = wallet.Balance,
                CreatedAtUtc = wallet.CreatedAtUtc
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("topup")]
    public async Task<IActionResult> TopUp([FromBody] TopUpRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        if (request.Amount <= 0)
            return BadRequestResponse("Amount must be greater than zero.");

        var newBalance = await walletService.TopUpAsync(
            userId.Value,
            request.Amount,
            request.Description ?? "Wallet top-up",
            cancellationToken
        );

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = "Wallet topped up successfully.",
            Data = new
            {
                Amount = request.Amount,
                NewBalance = newBalance,
                Description = request.Description ?? "Wallet top-up"
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        if (take > 100)         // API rate limiting
            take = 100;

        var transactions = await walletService.GetTransactionsAsync(userId.Value, skip, take, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Transactions fetched.",
            Data = transactions.Select(t => new
            {
                t.Id,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.RelatedPaymentId,
                t.CreatedAtUtc
            }),
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse();

        var wallet = await walletService.GetWalletAsync(userId.Value, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Balance fetched.",
            Data = new
            {
                Balance = wallet?.Balance ?? 0m,
                HasWallet = wallet != null
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }
}

public record TopUpRequest(decimal Amount, string? Description = null);
