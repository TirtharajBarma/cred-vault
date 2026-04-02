using BillingService.Application.Commands.Statements;
using BillingService.Application.Queries.Statements;
using BillingService.Infrastructure.Persistence.Sql;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/statements")]
[Authorize]
public class StatementsController(IMediator mediator) : BaseApiController
{
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

    [HttpGet("{statementId:guid}")]
    public async Task<IActionResult> GetStatementById(Guid statementId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetStatementByIdQuery(userId.Value, statementId), cancellationToken);
        return CreateResponse(result.Success, result.Statement, result.Message);
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateStatement(
        [FromBody] GenerateStatementRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var result = await mediator.Send(
            new GenerateStatementCommand(request.CardId, userId.Value, authHeader),
            cancellationToken);

        return CreateResponse(result.Success, new { statementId = result.StatementId }, result.Message);
    }

    [HttpPost("admin/generate")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> AdminGenerateStatement(
        [FromBody] AdminGenerateStatementRequest request,
        CancellationToken cancellationToken)
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var result = await mediator.Send(
            new GenerateStatementCommand(request.CardId, request.UserId, authHeader),
            cancellationToken);

        return CreateResponse(result.Success, new { statementId = result.StatementId }, result.Message);
    }

    [HttpGet("admin/all")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAllStatements(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllStatementsQuery(), cancellationToken);
        return CreateResponse(result.Success, result.Statements, result.Message);
    }

    [HttpGet("bill/{billId:guid}")]
    public async Task<IActionResult> GetStatementByBillId(Guid billId, CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetStatementByBillIdQuery(userId.Value, billId), cancellationToken);
        return CreateResponse(result.Success, result.Statements, result.Message);
    }

    [HttpGet("{statementId:guid}/transactions")]
    public async Task<IActionResult> GetStatementTransactions(Guid statementId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStatementTransactionsQuery(statementId), cancellationToken);
        return CreateResponse(result.Success, result.Transactions, result.Message);
    }

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

public record GenerateStatementRequest(Guid CardId);
public record AdminGenerateStatementRequest(Guid CardId, Guid UserId);
