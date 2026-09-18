using System.Security.Claims;
using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Common;
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

    /// <summary>
    /// A caller checking its own access has to get a real answer whether it has one or not — that
    /// is the whole point of the endpoint — so it is exempt from <see cref="RequireCompanyAccessFilter"/>.
    /// </summary>
    [HttpGet("me/companies/{companyId:guid}/role")]
    [AllowAnyCompany]
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

    /// <summary>
    /// Checks an arbitrary user's role in an arbitrary company — meant for service to service
    /// checks, never for a regular client, so it is restricted to SuperAdmin. It is also exempt
    /// from <see cref="RequireCompanyAccessFilter"/>: that filter checks the caller's own company
    /// membership, which is beside the point here, since the whole request is about someone else's.
    /// </summary>
    [HttpPost("check-role")]
    [Authorize(Policy = Policies.Admin)]
    [AllowAnyCompany]
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
            User.IsInRole(Constants.Roles.SuperAdmin),
            User.IsInRole(Constants.Roles.User),
            claims));
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(Constants.Claims.Subject)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
