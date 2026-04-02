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

[ApiController]
[Route("api/v1/billing/bills")]
[Authorize]
public class BillsController(IMediator mediator) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> ListMyBills(CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var result = await mediator.Send(new GetMyBillsQuery(userId.Value), cancellationToken);
        return CreateResponse(result.Success, result.Data, result.Message);
    }

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
        public string Currency { get; set; } = "USD";
    }

    [HttpPost("admin/generate-bill")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GenerateBill(
        [FromBody] GenerateBillRequest request,
        CancellationToken cancellationToken)
    {
        var authHeader = HttpContext.Request.Headers.Authorization.ToString();
        var adminUserId = GetUserIdFromToken() ?? Guid.Empty;

        var command = new GenerateAdminBillCommand(adminUserId, request.UserId, request.CardId, request.Currency, authHeader);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "UserId and CardId are required." || result.Message == "Card network is invalid." || result.Message.Contains("Outstanding balance is 0"))
                return BadRequest(result);
            if (result.Message == "Card not found.")
                return NotFound(result);
            
            return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("admin/check-overdue")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CheckOverdueBills(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new CheckOverdueBillsCommand(), cancellationToken);
        return Ok(result);
    }
}
