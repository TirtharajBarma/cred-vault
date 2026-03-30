using System.Security.Claims;
using CardService.Application.Commands.Transactions;
using CardService.Application.Queries.Transactions;
using CardService.Domain.Entities;
using Shared.Contracts.Controllers;
using Shared.Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Shared.Contracts.Models;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/cards")]
[Authorize]
public class CardTransactionsController(IMediator mediator) : BaseApiController
{
    [HttpGet("{cardId:guid}/transactions")]
    public async Task<IActionResult> ListTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse("User identity is missing from token.");

        var query = new ListCardTransactionsQuery(UserId: userId.Value, CardId: cardId);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> ListAllUserTransactions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return UnauthorizedResponse("User identity is missing from token.");

        var query = new ListUserTransactionsQuery(userId.Value);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{cardId:guid}/transactions")]
    public async Task<IActionResult> AddTransaction(Guid cardId, [FromBody] AddTransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
             return UnauthorizedResponse("User identity is missing from token.");

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
}
