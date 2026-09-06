using System.Security.Claims;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

[ApiController]
[Route("api/access")]
[Authorize]
public sealed class AccessController(IUserCompanyService userCompanyService) : ControllerBase
{
    [HttpGet("me/companies")]
    public async Task<ActionResult<IReadOnlyList<UserCompanyDto>>> GetMyCompanies(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var companies = await userCompanyService.GetUserCompaniesAsync(userId, cancellationToken);
        return Ok(companies);
    }

    [HttpGet("me/companies/{companyId:guid}/role")]
    public async Task<ActionResult<UserCompanyRoleDto>> GetMyRole(Guid companyId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
            return Unauthorized();

        var role = await userCompanyService.GetUserRoleAsync(userId, companyId, cancellationToken);
        if (string.IsNullOrWhiteSpace(role))
            return NotFound();

        return Ok(new UserCompanyRoleDto(companyId, role));
    }

    [HttpPost("check-role")]
    public async Task<ActionResult<CheckRoleResponse>> CheckRole([FromBody] CheckRoleRequest request, CancellationToken cancellationToken)
    {
        var allowed = await userCompanyService.HasRoleAsync(request.UserId, request.CompanyId, request.Role, cancellationToken);
        return Ok(new CheckRoleResponse(allowed));
    }

    /// <summary>
    /// Claims carried by the caller access token. Diagnostic endpoint: when an endpoint answers
    /// 403, this says whether the token actually carries the expected roles or whether the
    /// session predates a permission change.
    /// </summary>
    [HttpGet("me/claims")]
    public ActionResult<AccessDiagnosticsDto> GetMyClaims()
    {
        var claims = User.Claims
            .Select(claim => new ClaimDto(claim.Type, claim.Value))
            .OrderBy(claim => claim.Type)
            .ToList();

        return Ok(new AccessDiagnosticsDto(
            GetCurrentUserId(),
            User.Identity?.Name,
            User.IsInRole("SuperAdmin"),
            User.IsInRole("Admin"),
            claims));
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
