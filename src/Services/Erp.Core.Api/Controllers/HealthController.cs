using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Core.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get()
    {
        return Ok(new { service = "Core.Api", status = "healthy" });
    }
}
