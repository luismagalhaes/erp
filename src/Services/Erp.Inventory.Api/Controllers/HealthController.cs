using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Inventory.Api.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Liveness probe for the Inventory service.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new { service = "Inventory.Api", status = "healthy" });
}
