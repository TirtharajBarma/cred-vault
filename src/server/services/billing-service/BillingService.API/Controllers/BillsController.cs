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

    public sealed class MarkPaidRequest
    {
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// Internal endpoint — called only by the PaymentCompletedConsumer (RabbitMQ).
    /// Users must initiate payments via POST /api/v1/payments/initiate instead.
    /// Kept for admin/testing purposes only.
    /// </summary>
    [HttpPost("{billId:guid}/mark-paid")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> MarkBillPaid(
        Guid billId,
        [FromBody] MarkPaidRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserIdFromToken();
        if (userId is null) return UnauthorizedResponse();

        var command = new MarkBillPaidCommand(userId.Value, billId, request.Amount);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "Bill not found.") return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }
}
