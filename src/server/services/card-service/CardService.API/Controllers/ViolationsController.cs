using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Queries.Cards;
using CardService.Domain.Entities;
using Shared.Contracts.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/cards/admin")]
[Authorize]
public class ViolationsController(
    IMediator mediator,
    ICardRepository cards,
    IViolationRepository violations
) : ControllerBase
{
    [HttpGet("blocked")]
    [ProducesResponseType(typeof(ApiResponse<List<BlockedCardDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBlockedCards(CancellationToken ct)
    {
        var result = await mediator.Send(new GetBlockedCardsQuery(), ct);
        return Ok(result);
    }

    [HttpPost("{cardId}/unblock")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnblockCard(Guid cardId, CancellationToken ct)
    {
        var result = await mediator.Send(new UnblockCardCommand(cardId), ct);
        return Ok(result);
    }

    [HttpGet("{cardId}/violations")]
    [ProducesResponseType(typeof(ApiResponse<List<CardViolation>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCardViolations(Guid cardId, CancellationToken ct)
    {
        var card = await cards.GetByIdAsync(cardId, ct);
        if (card == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Card not found" });
        }

        var cardViolations = await violations.GetViolationsByCardIdAsync(cardId, ct);
        return Ok(new ApiResponse<List<CardViolation>> { Success = true, Data = cardViolations });
    }

    [HttpPost("{cardId}/violations/clear")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClearCardViolations(Guid cardId, CancellationToken ct)
    {
        var card = await cards.GetByIdAsync(cardId, ct);
        if (card == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Card not found" });
        }

        var result = await mediator.Send(new ClearStrikesCommand(cardId), ct);
        return Ok(result);
    }
}
