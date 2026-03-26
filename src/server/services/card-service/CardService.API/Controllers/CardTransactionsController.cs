using System.Security.Claims;
using CardService.Application.Abstractions.Persistence;
using CardService.Domain.Entities;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Models;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/cards/{cardId:guid}/transactions")]
[Authorize]
public class CardTransactionsController : ControllerBase
{
    private readonly ICardRepository _cardRepository;
    private readonly CardDbContext _db;

    public CardTransactionsController(ICardRepository cardRepository, CardDbContext db)
    {
        _cardRepository = cardRepository;
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> AddTransaction(Guid cardId, [FromBody] AddTransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "User identity is missing from token.", TraceId = HttpContext.TraceIdentifier });

        var isAdmin = User.IsInRole("admin");

        // Admins (e.g. BillingService forwarding payment) can post to any card; users only their own.
        CreditCard? card;
        if (isAdmin)
            card = await _cardRepository.GetByIdAsync(cardId, cancellationToken);
        else
            card = await _cardRepository.GetByUserAndIdAsync(userId.Value, cardId, cancellationToken);

        if (card is null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Card not found.", TraceId = HttpContext.TraceIdentifier });

        if (request.Amount <= 0)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Amount must be greater than 0.", TraceId = HttpContext.TraceIdentifier });

        if (request.Type == TransactionType.Purchase)
        {
            if (card.OutstandingBalance + request.Amount > card.CreditLimit)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Transaction declined: Insufficient credit limit.", TraceId = HttpContext.TraceIdentifier });

            card.OutstandingBalance += request.Amount;
        }
        else
        {
            card.OutstandingBalance = Math.Max(card.OutstandingBalance - request.Amount, 0);
        }

        var txn = new CardTransaction
        {
            Id = Guid.NewGuid(),
            CardId = cardId,
            UserId = card.UserId,
            Type = request.Type,
            Amount = request.Amount,
            Description = request.Description ?? string.Empty,
            DateUtc = request.DateUtc ?? DateTime.UtcNow
        };

        card.UpdatedAtUtc = DateTime.UtcNow;

        // Stage the transaction in the same DbContext, then let UpdateAsync flush everything in one SaveChanges.
        _db.CardTransactions.Add(txn);
        await _cardRepository.UpdateAsync(card, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<CardTransaction> { Success = true, Message = "Transaction added.", Data = txn, TraceId = HttpContext.TraceIdentifier });
    }

    [HttpGet]
    public async Task<IActionResult> ListTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "User identity is missing from token.", TraceId = HttpContext.TraceIdentifier });

        var txns = await _db.CardTransactions
            .AsNoTracking()
            .Where(x => x.CardId == cardId && x.UserId == userId.Value)
            .OrderByDescending(x => x.DateUtc)
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<List<CardTransaction>> { Success = true, Message = "Transactions fetched.", Data = txns, TraceId = HttpContext.TraceIdentifier });
    }

    private Guid? GetUserIdFromToken()
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return Guid.TryParse(claimValue, out var id) ? id : null;
    }

    public class AddTransactionRequest
    {
        public TransactionType Type { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public DateTime? DateUtc { get; set; }
    }
}
