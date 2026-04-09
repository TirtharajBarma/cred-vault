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
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIssuer(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetIssuerByIdQuery(id), cancellationToken);

        if (result.Id == Guid.Empty)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Issuer not found",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<CardIssuerDto>
        {
            Success = true,
            Message = "Issuer retrieved successfully",
            Data = result,
            TraceId = HttpContext.TraceIdentifier
        });
    }

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
            new CreateIssuerCommand(request.Name, request.Network),
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

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UpdateIssuer(
        [FromRoute] Guid id,
        [FromBody] CreateIssuerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateIssuerCommand(id, request.Name, request.Network),
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
        return Ok(new ApiResponse<CardIssuerDto>
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
        var deleteResult = await mediator.Send(new DeleteIssuerCommand(id), cancellationToken);

        return StatusCode(deleteResult.Success ? 200 : 400, new ApiResponse<object>
        {
            Success = deleteResult.Success,
            Message = deleteResult.Message,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
