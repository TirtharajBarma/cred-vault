using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Models;

namespace BillingService.API.Controllers;

[ApiController]
[Route("api/v1/billing/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Billing service is healthy.",
            Data = "Billing Service Healthy",
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
