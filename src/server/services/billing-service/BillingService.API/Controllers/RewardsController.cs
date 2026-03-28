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
        public decimal MinSpend { get; set; }
        public decimal RewardRate { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
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
            request.MinSpend,
            request.RewardRate,
            request.EffectiveFromUtc,
            request.EffectiveToUtc);

        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success) return BadRequest(result);

        return StatusCode(StatusCodes.Status201Created, result);
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
    public async Task<IActionResult> ListMyRewardTransactions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new ListMyRewardTransactionsQuery(userId.Value), cancellationToken);
        return Ok(result);
    }
}
