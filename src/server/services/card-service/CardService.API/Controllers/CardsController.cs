using Shared.Contracts.Controllers;
using CardService.Application.Commands.Cards;
using CardService.Application.Commands.Transactions;
using CardService.Application.Common;
using CardService.Domain.Entities;
using Shared.Contracts.DTOs.Card.Requests;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Application.Queries.Cards;
using CardService.Application.Queries.Transactions;
using CardService.Application.Abstractions.Persistence;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardService.API.Controllers;

/// <summary>
/// Credit card management controller providing endpoints for card CRUD operations.
/// Handles user card management (add, list, update, delete) and transaction recording.
/// Uses JWT authentication - user ID extracted from Bearer token.
/// Admin endpoints require admin role for administrative operations.
/// </summary>
/// <remarks>
/// User endpoints (requires authentication):
/// - POST /: Add a new credit card
/// - GET /: List all user's cards
/// - GET /transactions: List all transactions across cards
/// - GET /{cardId}: Get specific card details
/// - GET /{cardId}/transactions: List transactions for a specific card
/// - POST /{cardId}/transactions: Add transaction to a card
/// - PUT /{cardId}: Update card details (name, expiry, default)
/// - DELETE /{cardId}: Delete a card
///
/// Admin endpoints (requires admin role):
/// - GET /admin/{cardId}: Get card by ID (admin view)
/// - GET /admin/{cardId}/transactions: Get card transactions (admin view)
/// - PUT /{cardId}/admin: Update card as admin (credit limit, balance)
/// - GET /user/{userId}: Get all cards for a specific user
/// </remarks>
[Route("api/v1/cards")]
[Authorize]
public class CardsController(IMediator mediator, ICardRepository cardRepository, IDataProtectionProvider dataProtectionProvider) : BaseApiController
{
    private readonly IDataProtector cardNumberProtector = dataProtectionProvider.CreateProtector("CardService.CardNumber.v1");

    /// <summary>
    /// Add a new credit card for the authenticated user.
    /// Validates card number, expiry, and issuer exists.
    /// Detects card network (Visa/Mastercard) from card number prefix.
    /// </summary>
    /// <param name="request">CreateCardRequest with CardholderName, ExpMonth, ExpYear, CardNumber, IssuerId, IsDefault</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CardResult with created card details</returns>
    [HttpPost]
    public async Task<IActionResult> CreateCard([FromBody] CreateCardRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var digits = CardHelpers.DigitsOnly(request.CardNumber);
        if (string.IsNullOrWhiteSpace(digits))
        {
            return BadRequestResponse("Card number is required");
        }

        var encryptedCardNumber = cardNumberProtector.Protect(digits);          // built-in function -> .Protect()

        var result = await mediator.Send(
            new CreateCardCommand(
                userId.Value,
                request.CardholderName,
                request.ExpMonth,
                request.ExpYear,
                request.CardNumber,
                request.IssuerId,
                request.IsDefault,
                encryptedCardNumber
            ),
            cancellationToken);

        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode, StatusCodes.Status201Created);
    }

    /// <summary>
    /// List all credit cards belonging to the authenticated user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CardsResult with list of user's cards</returns>
    [HttpGet]
    public async Task<IActionResult> ListMyCards(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new ListMyCardsQuery(userId.Value), cancellationToken);
        return CreateResponse(result.Success, result.Cards, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// List all transactions across all user's cards.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of CardTransactionDto</returns>
    [HttpGet("transactions")]
    public async Task<IActionResult> ListAllTransactions(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new CardService.Application.Queries.Transactions.ListUserTransactionsQuery(userId.Value), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    /// <summary>
    /// List transactions for a specific card.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of transactions for the card</returns>
    [HttpGet("{cardId:guid}/transactions")]
    public async Task<IActionResult> ListCardTransactions(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var query = new ListCardTransactionsQuery(UserId: userId.Value, CardId: cardId);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Add a transaction to a card (purchase, payment, refund, etc).
    /// If admin, can add to any card. If user, only to their own cards.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="request">AddTransactionRequest with Type, Amount, Description, DateUtc</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with created transaction</returns>
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

    /// <summary>
    /// Get specific card details by ID.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CardResult with card details</returns>
    [HttpGet("{cardId:guid}")]
    public async Task<IActionResult> GetMyCardById(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyCardByIdQuery(userId.Value, cardId), cancellationToken);
        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode);
    }

    [HttpGet("{cardId:guid}/full-number")]
    public async Task<IActionResult> GetMyCardFullNumber(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var card = await cardRepository.GetByUserAndIdAsync(userId.Value, cardId, cancellationToken);
        if (card is null)
        {
            return NotFoundResponse("Card not found");
        }

        if (string.IsNullOrWhiteSpace(card.EncryptedCardNumber))
        {
            return BadRequestResponse("Full card number is not available for this card");
        }

        string number;
        try
        {
            number = cardNumberProtector.Unprotect(card.EncryptedCardNumber);
        }
        catch
        {
            return BadRequestResponse("Could not reveal card number");
        }

        if (number.Length < 13 || number.Length > 19 || number.Any(ch => ch < '0' || ch > '9'))
        {
            return BadRequestResponse("Stored card number is invalid");
        }

        return CreateResponse(true, new { cardNumber = number }, "Card number revealed");
    }

    /// <summary>
    /// Update card details (cardholder name, expiry, default status).
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="request">UpdateCardRequest with CardholderName, ExpMonth, ExpYear, IsDefault</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CardResult with updated card</returns>
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
                request.IsDefault
            ),
            cancellationToken);

        return CreateResponse(result.Success, result.Card, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Delete a credit card.
    /// Cannot delete if card has outstanding balance or pending bills.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OperationResult indicating success/failure</returns>
    [HttpDelete("{cardId:guid}")]
    public async Task<IActionResult> DeleteCard(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new DeleteCardCommand(userId.Value, cardId), cancellationToken);
        return CreateResponse(result.Success, result, result.Message, result.ErrorCode);
    }

    /// <summary>
    /// Admin: Get any card by ID.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with card data</returns>
    [HttpGet("admin/{cardId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGetCardById(Guid cardId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new AdminGetCardByIdQuery(cardId), cancellationToken);
        if (!result.Success) return CreateResponse(false, (object?)null, result.Message);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    /// <summary>
    /// Admin: Get transactions for any card.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with transactions</returns>
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

    /// <summary>
    /// Admin: Update card as administrator.
    /// Allows modifying credit limit, outstanding balance, billing cycle.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="request">UpdateCardByAdminRequest</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with updated card</returns>
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

    /// <summary>
    /// Admin: Get all cards for a specific user.
    /// </summary>
    /// <param name="userId">User's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of user's cards</returns>
    [HttpGet("user/{userId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetCardsByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCardsByUserIdQuery(userId), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }
}
