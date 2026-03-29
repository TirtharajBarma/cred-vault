using Shared.Contracts.Models;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/v1/identity/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Identity service is healthy.",
            Data = "Identity Service Healthy",
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
