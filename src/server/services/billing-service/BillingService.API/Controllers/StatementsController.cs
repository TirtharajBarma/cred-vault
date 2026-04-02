using BillingService.Application.Commands.Statements;
using BillingService.Application.Queries.Statements;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Controllers;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/statements")]
[Authorize]
public class StatementsController(IMediator mediator) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetMyStatements(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyStatementsQuery(userId.Value), cancellationToken);
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
        return CreateResponse(result.Success, result.Statement, result.Message);
    }
}

public record GenerateStatementRequest(Guid CardId);
public record AdminGenerateStatementRequest(Guid CardId, Guid UserId);
