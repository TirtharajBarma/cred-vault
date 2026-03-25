using CardService.Application.DTOs.Requests;
using CardService.Application.DTOs.Responses;
using CardService.Domain.Entities;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Models;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/issuers")]
[Authorize]
public class IssuersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListIssuers([FromServices] CardDbContext dbContext, CancellationToken cancellationToken)
    {
        var issuers = await dbContext.CardIssuers
            .AsNoTracking()
            .OrderBy(x => x.Network)
            .ThenBy(x => x.Name)
            .Select(x => new CardIssuerDto
            {
                Id = x.Id,
                Name = x.Name,
                Network = x.Network.ToString(),
                IsActive = x.IsActive,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<List<CardIssuerDto>>
        {
            Success = true,
            Message = "Issuers fetched successfully.",
            Data = issuers,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateIssuer(
        [FromServices] CardDbContext dbContext,
        [FromBody] CreateIssuerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Network))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Name and Network are required.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        if (!TryParseNetwork(request.Network, out var network) || network == CardNetwork.Unknown)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Network must be Visa or Mastercard.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var exists = await dbContext.CardIssuers.AnyAsync(x => x.Network == network, cancellationToken);
        if (exists)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Issuer for this network already exists.",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var now = DateTime.UtcNow;

        var issuer = new CardIssuer
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Network = network,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.CardIssuers.Add(issuer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, new ApiResponse<CardIssuerDto>
        {
            Success = true,
            Message = "Issuer created successfully.",
            Data = new CardIssuerDto
            {
                Id = issuer.Id,
                Name = issuer.Name,
                Network = issuer.Network.ToString(),
                IsActive = issuer.IsActive,
                CreatedAtUtc = issuer.CreatedAtUtc,
                UpdatedAtUtc = issuer.UpdatedAtUtc
            },
            TraceId = HttpContext.TraceIdentifier
        });
    }

    private static bool TryParseNetwork(string input, out CardNetwork network)
    {
        // Allow numeric values ("1"/"2") and names ("Visa"/"Mastercard").
        if (int.TryParse(input, out var networkInt) && Enum.IsDefined(typeof(CardNetwork), networkInt))
        {
            network = (CardNetwork)networkInt;
            return true;
        }

        return Enum.TryParse(input, ignoreCase: true, out network);
    }
}
