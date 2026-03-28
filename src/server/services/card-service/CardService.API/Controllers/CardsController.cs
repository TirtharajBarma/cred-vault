using Shared.Contracts.Controllers;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Common;
using CardService.Application.DTOs.Requests;
using CardService.Application.Queries.Cards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardService.API.Controllers;

[Route("api/v1/cards")]
[Authorize]
public class CardsController(IMediator mediator) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(
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
                request.IsDefault,
                Request.Headers.Authorization.ToString() ?? string.Empty),
            cancellationToken);

        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode, StatusCodes.Status201Created);
    }

    [HttpGet]
    public async Task<IActionResult> ListMyCards(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new ListMyCardsQuery(userId.Value), cancellationToken);
        return CreateResponse(result.Success, result.Cards, result.Message, result.ErrorCode);
    }

    [HttpGet("{cardId:guid}")]
    public async Task<IActionResult> GetMyCardById(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyCardByIdQuery(userId.Value, cardId), cancellationToken);
        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode);
    }

    [HttpPut("{cardId:guid}")]
    public async Task<IActionResult> UpdateCard(Guid cardId, [FromBody] UpdateCardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(
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

        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode);
    }

    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> DeleteCard(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new DeleteCardCommand(userId.Value, cardId), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    [HttpGet("admin/{cardId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetCardById(
        Guid cardId,
        [FromServices] ICardRepository cardRepository,
        CancellationToken cancellationToken)
    {
        var card = await cardRepository.GetByIdAsync(cardId, cancellationToken);
        if (card is null) return CreateResponse(false, (object?)null, "Card not found.", "CardNotFound");

        return CreateResponse(true, CardMapping.ToDto(card), "Card fetched successfully.");
    }
}

