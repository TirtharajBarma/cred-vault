using System.Security.Claims;
using BillingService.Domain.Entities;
using BillingService.Infrastructure.Persistence.Sql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/rewards")]
[Authorize]
public class RewardsController : ControllerBase
{
    public sealed class CreateRewardTierRequest
    {
        public string CardNetwork { get; set; } = string.Empty;
        public Guid? IssuerId { get; set; }
        public decimal MinSpend { get; set; }
        public decimal RewardRate { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
    }

    [HttpGet("tiers")]
    public async Task<IActionResult> ListRewardTiers(
        [FromServices] BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tiers = await dbContext.RewardTiers
            .AsNoTracking()
            .OrderBy(x => x.CardNetwork)
            .ThenByDescending(x => x.MinSpend)
            .ThenByDescending(x => x.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<List<RewardTier>>
        {
            Success = true,
            Message = "Reward tiers fetched successfully.",
            Data = tiers,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost("tiers")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateRewardTier(
        [FromServices] BillingDbContext dbContext,
        [FromBody] CreateRewardTierRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseCardNetwork(request.CardNetwork, out var network) || network == CardNetwork.Unknown)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "CardNetwork must be Visa or Mastercard.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (request.MinSpend < 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "MinSpend must be >= 0.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (request.RewardRate < 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "RewardRate must be >= 0.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var now = DateTime.UtcNow;

        var tier = new RewardTier
        {
            Id = Guid.NewGuid(),
            CardNetwork = network,
            IssuerId = request.IssuerId,
            MinSpend = request.MinSpend,
            RewardRate = request.RewardRate,
            EffectiveFromUtc = request.EffectiveFromUtc,
            EffectiveToUtc = request.EffectiveToUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.RewardTiers.Add(tier);
        await dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<RewardTier>
        {
            Success = true,
            Message = "Reward tier created successfully.",
            Data = tier,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetMyRewardAccount(
        [FromServices] BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var account = await dbContext.RewardAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

        return Ok(new ApiResponse<RewardAccount?>
        {
            Success = true,
            Message = "Reward account fetched successfully.",
            Data = account,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> ListMyRewardTransactions(
        [FromServices] BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var accountId = await dbContext.RewardAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (accountId is null)
        {
            return Ok(new ApiResponse<List<RewardTransaction>>
            {
                Success = true,
                Message = "No reward account found for user.",
                Data = [],
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var txs = await dbContext.RewardTransactions
            .AsNoTracking()
            .Where(x => x.RewardAccountId == accountId.Value)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<List<RewardTransaction>>
        {
            Success = true,
            Message = "Reward transactions fetched successfully.",
            Data = txs,
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
