using BillingService.Application.Queries.Statements;
using BillingService.Infrastructure.Persistence.Sql;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;

namespace BillingService.API.Controllers;

/// <summary>
/// Statements controller handling credit card statement operations.
/// Manages statement viewing and retrieval of statement transactions.
/// Statements summarize a billing cycle's transactions and payments.
/// </summary>
/// <remarks>
/// User endpoints (requires authentication):
/// - GET /: List all statements for user
/// - GET /{statementId}: Get specific statement details
/// - GET /bill/{billId}: Get statements linked to a bill
/// - GET /{statementId}/transactions: Get transactions in a statement
///
/// Admin endpoints (requires admin role):
/// - GET /admin/all: Get all statements
/// - GET /admin/{statementId}/full: Get full statement details with bill and transactions
/// </remarks>
[ApiController]
[Route("api/v1/billing/statements")]
[Authorize]
public class StatementsController(IMediator mediator) : BaseApiController
{
    /// <summary>
    /// List all statements for the authenticated user (or specified user if admin).
    /// Returns statements ordered by date descending (newest first).
    /// </summary>
    /// <param name="userId">Optional user ID (admin only - to view other user's statements)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of statements</returns>
    [HttpGet]
    public async Task<IActionResult> GetMyStatements(
        [FromQuery] Guid? userId,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetUserIdFromToken();
        if (currentUserId is null) return UnauthorizedResponse();

        var targetUserId = (User.IsInRole("admin") && userId.HasValue) ? userId.Value : currentUserId.Value;

        var result = await mediator.Send(new GetMyStatementsQuery(targetUserId), cancellationToken);
        return CreateResponse(result.Success, result.Statements, result.Message);
    }

    /// <summary>
    /// Get specific statement by ID.
    /// </summary>
    /// <param name="statementId">Statement's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with statement details</returns>
    [HttpGet("{statementId:guid}")]
    public async Task<IActionResult> GetStatementById(Guid statementId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetStatementByIdQuery(userId.Value, statementId), cancellationToken);
        return CreateResponse(result.Success, result.Statement, result.Message);
    }

    /// <summary>
    /// Admin: Get all statements across all users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of all statements</returns>
    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllStatements(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllStatementsQuery(), cancellationToken);
        return CreateResponse(result.Success, result.Statements, result.Message);
    }

    /// <summary>
    /// Get statements linked to a specific bill.
    /// </summary>
    /// <param name="billId">Bill's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of statements</returns>
    [HttpGet("bill/{billId:guid}")]
    public async Task<IActionResult> GetStatementByBillId(Guid billId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetStatementByBillIdQuery(userId.Value, billId), cancellationToken);
        return CreateResponse(result.Success, result.Statements, result.Message);
    }

    /// <summary>
    /// Get all transactions within a specific statement.
    /// </summary>
    /// <param name="statementId">Statement's unique GUID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with list of statement transactions</returns>
    [HttpGet("{statementId:guid}/transactions")]
    public async Task<IActionResult> GetStatementTransactions(Guid statementId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStatementTransactionsQuery(statementId), cancellationToken);
        return CreateResponse(result.Success, result.Transactions, result.Message);
    }

    /// <summary>
    /// Admin: Get full statement details including statement, bill, transactions, and rewards.
    /// </summary>
    /// <param name="statementId">Statement's unique GUID</param>
    /// <param name="db">BillingDbContext for direct database access</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>ApiResponse with complete statement data</returns>
    [HttpGet("admin/{statementId:guid}/full")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAdminStatementFull(
        Guid statementId,
        [FromServices] BillingDbContext db,
        CancellationToken cancellationToken)
    {
        var statement = await db.Statements
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == statementId, cancellationToken);

        if (statement is null)
        {
            return CreateResponse(false, (object?)null, "Statement not found.", "NotFound", StatusCodes.Status404NotFound);
        }

        var bill = statement.BillId.HasValue
            ? await db.Bills.AsNoTracking().FirstOrDefaultAsync(x => x.Id == statement.BillId.Value, cancellationToken)
            : null;

        var transactions = await db.StatementTransactions
            .AsNoTracking()
            .Where(x => x.StatementId == statementId)
            .OrderByDescending(x => x.DateUtc)
            .ToListAsync(cancellationToken);

        var rewards = bill is null
            ? new List<Domain.Entities.RewardTransaction>()
            : await db.RewardTransactions
                .AsNoTracking()
                .Where(x => x.BillId == bill.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

        return CreateResponse(true, new
        {
            statement,
            bill,
            transactions,
            rewards
        }, "Statement full details fetched.");
    }
}
