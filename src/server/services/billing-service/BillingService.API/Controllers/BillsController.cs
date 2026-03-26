using System.Security.Claims;
using BillingService.Domain.Entities;
using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/bills")]
[Authorize]
public class BillsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListMyBills(
        [FromServices] BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var bills = await dbContext.Bills
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.BillingDateUtc)
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<List<Bill>>
        {
            Success = true,
            Message = "Bills fetched successfully.",
            Data = bills,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("{billId:guid}")]
    public async Task<IActionResult> GetMyBillById(
        Guid billId,
        [FromServices] BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var bill = await dbContext.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == billId && x.UserId == userId.Value, cancellationToken);

        if (bill is null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Bill not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<Bill>
        {
            Success = true,
            Message = "Bill fetched successfully.",
            Data = bill,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    public sealed class GenerateBillRequest
    {
        public Guid UserId { get; set; }
        public Guid CardId { get; set; }
        public string Currency { get; set; } = "USD";
    }

    [HttpPost("admin/generate-bill")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GenerateBill(
        [FromServices] BillingDbContext dbContext,
        [FromServices] CardServiceClient cardServiceClient,
        [FromBody] GenerateBillRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.CardId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "UserId and CardId are required.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        // Fetch the card from CardService — forward the admin's own JWT.
        var (cardFetchSuccess, card, cardFetchError, cardFetchStatus) = await cardServiceClient.GetCardByIdForAdminAsync(
            request.CardId,
            HttpContext.Request.Headers.Authorization.ToString(),
            cancellationToken);

        if (!cardFetchSuccess)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiResponse<object>
            {
                Success = false,
                Message = cardFetchError ?? "Failed to fetch card from CardService.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (card is null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Card not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (!TryParseCardNetwork(card.Network, out var network) || network == CardNetwork.Unknown)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Card network is invalid.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var amount = card.OutstandingBalance;
        if (amount <= 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Outstanding balance is 0. No bill to generate.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        // MinDue = 10% of total amount, minimum $10
        var minDue = Math.Max(Math.Round(amount * 0.10m, 2, MidpointRounding.AwayFromZero), 10.00m);
        var now = DateTime.UtcNow;
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant();

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CardId = request.CardId,
            CardNetwork = network,
            IssuerId = card.IssuerId,
            Amount = amount,
            MinDue = minDue,
            Currency = currency,
            BillingDateUtc = now,
            DueDateUtc = now.AddDays(20),
            Status = BillStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Bills.Add(bill);
        await dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<Bill>
        {
            Success = true,
            Message = $"Bill generated. Amount: {amount} {currency}, MinDue: {minDue} {currency}, DueDate: {bill.DueDateUtc:yyyy-MM-dd}.",
            Data = bill,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    public sealed class MarkPaidRequest
    {
        public decimal Amount { get; set; }
    }

    [HttpPost("{billId:guid}/mark-paid")]
    public async Task<IActionResult> MarkBillPaid(
        Guid billId,
        [FromServices] BillingDbContext dbContext,
        [FromServices] CardServiceClient cardServiceClient,
        [FromBody] MarkPaidRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var bill = await dbContext.Bills.FirstOrDefaultAsync(x => x.Id == billId && x.UserId == userId.Value, cancellationToken);
        if (bill is null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Bill not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (bill.Status == BillStatus.Paid)
        {
            return Ok(new ApiResponse<Bill>
            {
                Success = true,
                Message = "Bill already paid.",
                Data = bill,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (request.Amount < bill.MinDue)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = $"Payment amount must be at least the Minimum Due ({bill.MinDue}).",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        // Notify CardService about this payment to lower the card's outstanding balance
        var (syncSuccess, syncError, syncStatusCode) = await cardServiceClient.AddTransactionAsync(
            bill.CardId,
            new CardServiceClient.AddTransactionRequest
            {
                Type = 2, // Payment
                Amount = request.Amount,
                Description = "Bill Payment"
            },
            HttpContext.Request.Headers.Authorization.ToString(),
            cancellationToken);

        if (!syncSuccess)
        {
            return StatusCode((int)(syncStatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable), new ApiResponse<object>
            {
                Success = false,
                Message = $"Failed to sync payment with Card Service: {syncError}",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var now = DateTime.UtcNow;

        bill.Status = BillStatus.Paid;
        bill.UpdatedAtUtc = now;

        // Rewards: prioritized synchronous computation.
        // 1. Matches Network
        // 2. Exact matches IssuerId OR IssuerId is null (fallback default)
        // 3. Prioritizes Issuer specific tier over default fallback
        var tier = await dbContext.RewardTiers
            .AsNoTracking()
            .Where(x => x.CardNetwork == bill.CardNetwork && 
                        (x.IssuerId == bill.IssuerId || x.IssuerId == null) &&
                        x.EffectiveFromUtc <= now && 
                        (x.EffectiveToUtc == null || x.EffectiveToUtc >= now) &&
                        request.Amount >= x.MinSpend)
            .OrderByDescending(x => x.IssuerId != null) // Prioritize Issuer-specific tiers over network defaults
            .ThenByDescending(x => x.MinSpend)
            .FirstOrDefaultAsync(cancellationToken);

        RewardAccount? account = null;
        if (tier is not null)
        {
            account = await dbContext.RewardAccounts.FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
            if (account is null)
            {
                account = new RewardAccount
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    RewardTierId = tier.Id,
                    PointsBalance = 0,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                dbContext.RewardAccounts.Add(account);
            }
            else
            {
                account.RewardTierId = tier.Id;
                account.UpdatedAtUtc = now;
            }

            var points = Math.Round(bill.Amount * tier.RewardRate, 2, MidpointRounding.AwayFromZero);
            if (points > 0)
            {
                account.PointsBalance += points;
                account.UpdatedAtUtc = now;

                dbContext.RewardTransactions.Add(new RewardTransaction
                {
                    Id = Guid.NewGuid(),
                    RewardAccountId = account.Id,
                    BillId = bill.Id,
                    Points = points,
                    Type = RewardTransactionType.Earned,
                    CreatedAtUtc = now
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<Bill>
        {
            Success = true,
            Message = "Bill marked as paid.",
            Data = bill,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    private ApiResponse<object> UnauthorizedResponse()
    {
        return new ApiResponse<object>
        {
            Success = false,
            Message = "User identity is missing from token.",
            TraceId = HttpContext.TraceIdentifier
        };
    }

    private Guid? GetUserIdFromToken()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return null;
        }

        return Guid.TryParse(claimValue, out var userId) ? userId : null;
    }

    private static bool TryParseCardNetwork(string input, out CardNetwork network)
    {
        if (int.TryParse(input, out var networkInt) && Enum.IsDefined(typeof(CardNetwork), networkInt))
        {
            network = (CardNetwork)networkInt;
            return true;
        }

        return Enum.TryParse(input, ignoreCase: true, out network);
    }
}
