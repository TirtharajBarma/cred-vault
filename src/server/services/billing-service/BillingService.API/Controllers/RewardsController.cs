using System.Security.Claims;
using BillingService.Application.Commands.Rewards;
using BillingService.Application.Queries.Rewards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

/// <summary>
/// Rewards controller handling loyalty points and reward tier management.
/// Manages reward accounts, point transactions, and reward tier configuration.
/// Users earn points on card transactions; points can be redeemed for bill payments or account credit.
/// </summary>
/// <remarks>
/// User endpoints (requires authentication):
/// - GET /tiers: View available reward tiers (for user reference)
/// - GET /account: Get user's reward account and points balance
/// - GET /transactions: Get user's reward transaction history
/// - POST /redeem: Redeem points for bill payment or account credit
///
/// Admin endpoints (requires admin role):
/// - POST /tiers: Create new reward tier
/// - PUT /tiers/{id}: Update reward tier
/// - DELETE /tiers/{id}: Delete reward tier
///
/// Internal endpoints:
/// - POST /internal/redeem: Service-to-service redemption (no auth required)
/// </remarks>
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

    /// <summary>
    /// List all reward tiers (current and historical).
    /// Shows all tiers with their minimum spend requirements and points rates.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of RewardTierDto</returns>
    [HttpGet("tiers")]
    public async Task<IActionResult> ListRewardTiers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListRewardTiersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Admin: Create a new reward tier.
    /// Reward tiers define points earning rates based on card network, issuer, and minimum spend.
    /// </summary>
    /// <param name="request">CreateRewardTierRequest with CardNetwork, IssuerId, MinimumSpend, PointsPerDollar, Effective dates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with created tier</returns>
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

    /// <summary>
    /// Admin: Update an existing reward tier.
    /// </summary>
    /// <param name="id">Reward tier's unique GUID</param>
    /// <param name="request">CreateRewardTierRequest with updated values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with updated tier</returns>
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

    /// <summary>
    /// Admin: Delete a reward tier.
    /// Cannot delete if tier is currently in use by user accounts.
    /// </summary>
    /// <param name="id">Reward tier's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse indicating success</returns>
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

    /// <summary>
    /// Get current user's reward account details.
    /// Returns points balance and account status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with reward account (points balance)</returns>
    [HttpGet("account")]
    public async Task<IActionResult> GetMyRewardAccount(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyRewardAccountQuery(userId.Value), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get user's reward transaction history.
    /// Admin can pass userId to view other user's history.
    /// </summary>
    /// <param name="userId">Optional user ID (admin only)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of reward transactions</returns>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetMyTransactions([FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        var currentUserId = GetUserIdFromToken();
        if (currentUserId is null) return UnauthorizedResponse();

        var targetUserId = (User.IsInRole("admin") && userId.HasValue) ? userId.Value : currentUserId.Value;

        var result = await mediator.Send(new GetRewardTransactionsQuery(targetUserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Redeem user's reward points.
    /// Can redeem for bill payment (requires BillId) or account credit.
    /// </summary>
    /// <param name="request">RedeemRewardsRequest with Points, Target (Bill/Account), BillId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with redemption result</returns>
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

    /// <summary>
    /// Internal: Redeem rewards for a user (called by other services).
    /// Used when paying bills - deducts points from user account.
    /// </summary>
    /// <param name="request">InternalRedeemRewardsRequest with UserId, Points, Target, BillId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with redemption result</returns>
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
