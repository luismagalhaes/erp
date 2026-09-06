using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Core.Api.Controllers;

[ApiController]
[Route("api/user-companies")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class UserCompaniesController(IUserCompanyAdminService userCompanyAdminService) : ControllerBase
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
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
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
