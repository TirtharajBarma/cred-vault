using Shared.Contracts.DTOs.Card.Requests;
using Shared.Contracts.DTOs.Card.Responses;
using CardService.Application.Commands.Issuers;
using CardService.Application.Queries.Cards;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Models;
using CardService.Application.Abstractions.Persistence;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/issuers")]
[Authorize]
public class IssuersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListIssuers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ListIssuersQuery(), cancellationToken);

        return Ok(new ApiResponse<List<CardIssuerDto>>
        {
            Success = result.Success,
            Message = result.Message,
            Data = result.Issuers,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateIssuer(
        [FromBody] CreateIssuerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateIssuerCommand(request.Name, request.Network, request.IsActive),
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = result.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var issuer = result.Issuers.First();
        return StatusCode(StatusCodes.Status201Created, new ApiResponse<CardIssuerDto>
        {
            Success = true,
            Message = result.Message,
            Data = issuer,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteIssuer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var issuer = await mediator.Send(new GetIssuerByIdQuery(id), cancellationToken);
        
        if (issuer == null || issuer.Id == Guid.Empty)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Issuer not found.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var deleteResult = await mediator.Send(new DeleteIssuerCommand(id), cancellationToken);

        if (!deleteResult.Success)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = deleteResult.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Issuer deleted successfully.",
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
