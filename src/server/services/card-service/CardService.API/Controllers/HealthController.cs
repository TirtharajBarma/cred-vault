using Shared.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace CardService.API.Controllers;

[ApiController]
[Route("api/v1/cards/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Card service is healthy.",
            Data = "Card Service Healthy",
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
