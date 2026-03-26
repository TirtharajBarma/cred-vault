using System.Security.Claims;
using Shared.Contracts.Models;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Common;
using CardService.Application.DTOs.Requests;
using CardService.Application.DTOs.Responses;
using CardService.Application.Queries.Cards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/cards")]
[Authorize]
public class CardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CardsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var result = await _mediator.Send(
            new CreateCardCommand(
                userId.Value,
                request.CardholderName,
                request.ExpMonth,
                request.ExpYear,
                request.CardNumber,
                request.IssuerId,
                request.CreditLimit,
                request.OutstandingBalance,
                request.BillingCycleStartDay,
                request.IsDefault),
            cancellationToken);

        return FromCardResult(result, StatusCodes.Status201Created);
    }

    [HttpGet("issuers")]
    [AllowAnonymous]
    public async Task<IActionResult> GetIssuers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListIssuersQuery(), cancellationToken);
        var response = BuildResponse(result.Success, result.Issuers, result.Message);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> ListMyCards(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var result = await _mediator.Send(new ListMyCardsQuery(userId.Value), cancellationToken);
        return FromCardsResult(result);
    }

    [HttpGet("{cardId:guid}")]
    public async Task<IActionResult> GetMyCardById(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var result = await _mediator.Send(new GetMyCardByIdQuery(userId.Value, cardId), cancellationToken);
        return FromCardResult(result, StatusCodes.Status200OK);
    }

    [HttpPut("{cardId:guid}")]
    public async Task<IActionResult> UpdateCard(Guid cardId, [FromBody] UpdateCardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var result = await _mediator.Send(
            new UpdateCardCommand(
                userId.Value,
                cardId,
                request.CardholderName,
                request.ExpMonth,
                request.ExpYear,
                request.CreditLimit,
                request.OutstandingBalance,
                request.BillingCycleStartDay,
                request.IsDefault),
            cancellationToken);

        return FromCardResult(result, StatusCodes.Status200OK);
    }

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> DeleteCard(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
        {
            return Unauthorized(UnauthorizedResponse());
        }

        var result = await _mediator.Send(new DeleteCardCommand(userId.Value, cardId), cancellationToken);
        return FromOperationResult(result);
    }

    // Admin-only: fetch any card by ID regardless of owner (used by BillingService)
    [HttpGet("admin/{cardId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetCardById(
        Guid cardId,
        [FromServices] ICardRepository cardRepository,
        CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(cardId, cancellationToken);
        if (card is null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Card not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(BuildResponse(true, CardMapping.ToDto(card), "Card fetched successfully."));
    }

    private IActionResult FromCardResult(CardResult result, int successStatusCode)
    {
        var response = BuildResponse(result.Success, result.Card, result.Message);

        if (result.Success)
        {
            return StatusCode(successStatusCode, response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.CardNotFound => NotFound(response),
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }

    private IActionResult FromCardsResult(CardsResult result)
    {
        var response = BuildResponse(result.Success, result.Cards, result.Message);

        if (result.Success)
        {
            return Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }

    private IActionResult FromOperationResult(OperationResult result)
    {
        var response = BuildResponse(result.Success, result, result.Message);

        if (result.Success)
        {
            return Ok(response);
        }

        return result.ErrorCode switch
        {
            ErrorCodes.CardNotFound => NotFound(response),
            ErrorCodes.ValidationError => BadRequest(response),
            ErrorCodes.Forbidden => StatusCode(StatusCodes.Status403Forbidden, response),
            _ => BadRequest(response)
        };
    }

    private ApiResponse<T> BuildResponse<T>(bool success, T data, string message)
    {
        return new ApiResponse<T>
        {
            Success = success,
            Message = message,
            Data = data,
            TraceId = HttpContext.TraceIdentifier
        };
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
}
