using System.Security.Claims;
using BillingService.Application.Commands.Rewards;
using BillingService.Application.Queries.Rewards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/rewards")]
[Authorize]
public class RewardsController(IMediator mediator) : BaseApiController
{
    public sealed class CreateRewardTierRequest
    {
        public string CardNetwork { get; set; } = string.Empty;
        public Guid? IssuerId { get; set; }
        public decimal MinimumSpend { get; set; }
        public decimal PointsPerDollar { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
    }

    public sealed class RedeemRewardsRequest
    {
        public int Points { get; set; }
        public string Target { get; set; } = "Bill";
        public Guid? BillId { get; set; }
    }

    public sealed class InternalRedeemRewardsRequest
    {
        public Guid UserId { get; set; }
        public int Points { get; set; }
        public string Target { get; set; } = "Bill";
        public Guid? BillId { get; set; }
    }

    [HttpGet("tiers")]
    public async Task<IActionResult> ListRewardTiers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListRewardTiersQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost("tiers")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateRewardTier(
        [FromBody] CreateRewardTierRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateRewardTierCommand(
            request.CardNetwork,
            request.IssuerId,
            request.MinimumSpend,
            request.PointsPerDollar,
            request.EffectiveFromUtc,
            request.EffectiveToUtc);

        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success) return BadRequest(result);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("tiers/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateRewardTier(
        [FromRoute] Guid id,
        [FromBody] CreateRewardTierRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRewardTierCommand(
            id,
            request.CardNetwork,
            request.IssuerId,
            request.MinimumSpend,
            request.PointsPerDollar,
            request.EffectiveFromUtc,
            request.EffectiveToUtc);

        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("tiers/{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteRewardTier(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteRewardTierCommand(id), cancellationToken);
        
        if (!result.Success) return NotFoundResponse(result.Message);

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = result.Message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("account")]
    public async Task<IActionResult> GetMyRewardAccount(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyRewardAccountQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetMyTransactions([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserIdFromToken();
        if (currentUserId is null) return UnauthorizedResponse();

        var targetUserId = (User.IsInRole("admin") && userId.HasValue) ? userId.Value : currentUserId.Value;

        var result = await mediator.Send(new GetRewardTransactionsQuery(targetUserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> RedeemRewards(
        [FromBody] RedeemRewardsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var target = request.Target?.ToLower() switch
        {
            "account" => RedeemRewardsTarget.Account,
            "bill" => RedeemRewardsTarget.Bill,
            _ => RedeemRewardsTarget.Bill
        };

        var command = new RedeemRewardsCommand(
            userId.Value,
            request.Points,
            target,
            request.BillId);

        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("internal/redeem")]
    [AllowAnonymous]  // Internal service-to-service call
    public async Task<IActionResult> InternalRedeemRewards(
        [FromBody] InternalRedeemRewardsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return BadRequestResponse("UserId is required");

        var target = request.Target?.ToLower() switch
        {
            "account" => RedeemRewardsTarget.Account,
            "bill" => RedeemRewardsTarget.Bill,
            _ => RedeemRewardsTarget.Bill
        };

        var command = new RedeemRewardsCommand(
            request.UserId,
            request.Points,
            target,
            request.BillId);

        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success) return BadRequestResponse(result.Message ?? "Redemption failed");

        return Ok(result);
    }
}
