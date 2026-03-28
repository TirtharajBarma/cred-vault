using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Models;

namespace PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/payments/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Health()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "Payment service is healthy.",
            Data = "Payment Service Healthy",
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
