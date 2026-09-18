using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

[ApiController]
[Route("api/user-companies")]
// Membership management is an admin-only surface end to end: who belongs to which company, and
// with what role, is exactly the information a SuperAdmin backoffice needs and nobody else does.
// The reads used to be Read-policy — any client with an erp.read scope, which is essentially every
// signed-in user — and, unlike every other companyId-scoped controller, GetAll had no companyId
// to check even when one was given (it happily listed every company's memberships when it was
// left out), so RequireCompanyAccessFilter could not have closed this one on its own.
[Authorize(Policy = Policies.Admin)]
public sealed class UserCompaniesController(IUserCompanyAdminService userCompanyAdminService, ILogger<UserCompaniesController> logger) : ControllerBase
{
    /// <summary>Lists user memberships, optionally limited to one company.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserCompanyAdminDto>>> GetAll(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken)
    {
        var result = await userCompanyAdminService.GetAllAsync(companyId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserCompanyAdminDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await userCompanyAdminService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserCompanyAdminDto>> Create([FromBody] CreateUserCompanyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await userCompanyAdminService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(UserCompaniesController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(UserCompaniesController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserCompanyAdminDto>> Update(Guid id, [FromBody] UpdateUserCompanyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await userCompanyAdminService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(UserCompaniesController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await userCompanyAdminService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
