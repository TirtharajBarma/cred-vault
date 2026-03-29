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
[Route("api/v1/cards/{cardId:guid}/transactions")]
[Authorize]
public class CardTransactionsController : BaseApiController
{
    private readonly IMediator _mediator;

    public CardTransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> AddTransaction(Guid cardId, [FromBody] AddTransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "User identity is missing from token.", TraceId = HttpContext.TraceIdentifier });

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

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Card not found.")
                return NotFound(new ApiResponse<object> { Success = false, Message = result.Message, TraceId = HttpContext.TraceIdentifier });
            
            return BadRequest(new ApiResponse<object> { Success = false, Message = result.Message, TraceId = HttpContext.TraceIdentifier });
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    public async Task<IActionResult> ListTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "User identity is missing from token.", TraceId = HttpContext.TraceIdentifier });

        var query = new ListCardTransactionsQuery(UserId: userId.Value, CardId: cardId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    public class AddTransactionRequest
    {
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime? DateUtc { get; set; }
    }
}
