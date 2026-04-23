using System.Security.Claims;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Queries.Bills;
using BillingService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

/// <summary>
/// Billing controller handling bill-related operations for credit card bills.
/// Manages bill generation, listing, viewing, and admin functions like generating bills for users.
/// Uses JWT authentication - regular users see their own bills, admins can view any user's bills.
/// </summary>
/// <remarks>
/// User endpoints (requires authentication):
/// - GET /: List all bills for current user
/// - GET /{billId}: Get specific bill details
///
/// Admin endpoints (requires admin role):
/// - POST /admin/generate-bill: Generate a bill for a user (creates billing cycle)
/// - POST /admin/check-overdue: Check and update status of overdue bills
///
/// Public endpoints:
/// - GET /has-pending/{cardId}: Check if card has pending bill (used by CardService)
/// </remarks>
[ApiController]
[Route("api/v1/billing/bills")]
[Authorize]
public class BillsController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// List all bills for the authenticated user (or specified user if admin).
    /// Admin can pass userId query parameter to view other user's bills.
    /// </summary>
    /// <param name="userId">Optional user ID (admin only - to view other user's bills)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of bills</returns>
    [HttpGet]
    public async Task<IActionResult> ListMyBills(
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserIdFromToken();
        if (currentUserId is null) return UnauthorizedResponse();

        var targetUserId = (User.IsInRole("admin") && userId.HasValue) ? userId.Value : currentUserId.Value;

        var result = await mediator.Send(new GetMyBillsQuery(targetUserId), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

    /// <summary>
    /// Get specific bill details by ID.
    /// User can only view their own bills.
    /// </summary>
    /// <param name="billId">Bill's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with bill details</returns>
    [HttpGet("{billId:guid}")]
    public async Task<IActionResult> GetMyBillById(Guid billId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyBillByIdQuery(userId.Value, billId), cancellationToken);
        
        return CreateResponse(result.Success, result.Data, result.Message, result.Success ? null : "NotFound");
    }

    public sealed class GenerateBillRequest
    {
        public Guid UserId { get; set; }
        public Guid CardId { get; set; }
        public string Currency { get; set; } = "INR";
    }

    /// <summary>
    /// Admin: Generate a billing cycle bill for a user.
    /// Creates a new bill based on card's outstanding balance and billing cycle.
    /// </summary>
    /// <param name="request">GenerateBillRequest with UserId, CardId, Currency</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with generated bill</returns>
    [HttpPost("admin/generate-bill")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GenerateBill(
        [FromBody] GenerateBillRequest request,
        CancellationToken cancellationToken)
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();      // extract JWT token from req to be used later
        var adminUserId = GetUserIdFromToken() ?? Guid.Empty;

        var command = new GenerateAdminBillCommand(adminUserId, request.UserId, request.CardId, request.Currency, authHeader);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "UserId and CardId are required." || result.Message == "Invalid card network" || result.Message == "No balance to bill")
                return BadRequest(result);
            if (result.Message == "Card not found")
                return NotFound(result);
            if (result.Message == "A pending bill already exists for this card")
                return Conflict(result);
            
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        var bill = result.Data;
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<object>
        {
            Success = true,
            Message = result.Message,
            Data = bill is null
                ? null
                : new
                {
                    bill.Id,
                    bill.UserId,
                    bill.CardId,
                    bill.Amount,
                    bill.MinDue,
                    bill.Currency,
                    bill.BillingDateUtc,
                    bill.DueDateUtc,
                    bill.Status,
                    bill.CreatedAtUtc,
                    bill.UpdatedAtUtc
                },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    /// <summary>
    /// Admin: Check for overdue bills and update their status.
    /// Scheduled task that runs to mark pending bills as overdue if past due date.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with count of updated bills</returns>
    [HttpPost("admin/check-overdue")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CheckOverdueBills(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CheckOverdueBillsCommand(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Check if a card has any pending or overdue bill.
    /// Used by CardService to determine if card can be deleted.
    /// </summary>
    /// <param name="cardId">Card's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Boolean indicating if card has pending bill</returns>
    [HttpGet("has-pending/{cardId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> HasPendingBill(Guid cardId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new HasPendingBillQuery(cardId), cancellationToken);
        return Ok(result);
    }
}
