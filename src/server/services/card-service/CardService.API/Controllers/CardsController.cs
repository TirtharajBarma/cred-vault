using Shared.Contracts.Controllers;
using CardService.Application.Commands.Cards;
using CardService.Application.Commands.Transactions;
using CardService.Application.Common;
using CardService.Domain.Entities;
using Shared.Contracts.DTOs.Card.Requests;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Application.Queries.Cards;
using CardService.Application.Queries.Transactions;
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
                request.IsDefault),
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

    [HttpGet("transactions")]
    public async Task<IActionResult> ListAllTransactions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new CardService.Application.Queries.Transactions.ListUserTransactionsQuery(userId.Value), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    [HttpGet("{cardId:guid}/transactions")]
    public async Task<IActionResult> ListCardTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var query = new ListCardTransactionsQuery(UserId: userId.Value, CardId: cardId);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{cardId:guid}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid cardId, [FromBody] AddTransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var isAdmin = User.IsInRole("admin");

        var command = new AddCardTransactionCommand(
            UserId: userId.Value,
            CardId: cardId,
            Type: request.Type,
            Amount: request.Amount,
            Description: request.Description,
            DateUtc: request.DateUtc,
            IsAdmin: isAdmin
        );

        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Card not found.")
                return NotFoundResponse(result.Message);
            
            return BadRequestResponse(result.Message);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    public class AddTransactionRequest
    {
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime? DateUtc { get; set; }
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
    public async Task<IActionResult> AdminGetCardById(Guid cardId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AdminGetCardByIdQuery(cardId), cancellationToken);
        if (!result.Success) return CreateResponse(false, (object?)null, result.Message);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    [HttpGet("admin/{cardId:guid}/transactions")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetCardTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AdminGetCardTransactionsQuery(cardId), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    public sealed class UpdateCardByAdminRequest
    {
        public string? CardholderName { get; set; }
        public decimal CreditLimit { get; set; }
        public decimal? OutstandingBalance { get; set; }
        public int? BillingCycleStartDay { get; set; }
    }

    [HttpPut("{cardId:guid}/admin")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateCardByAdmin(
        Guid cardId,
        [FromBody] UpdateCardByAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateCardByAdminCommand(
            cardId,
            request.CardholderName,
            request.CreditLimit,
            request.OutstandingBalance,
            request.BillingCycleStartDay
        ), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetCardsByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCardsByUserIdQuery(userId), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }
}
