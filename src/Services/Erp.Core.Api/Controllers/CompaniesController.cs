using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Core.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Roles = "Admin,SuperAdmin")]
public sealed class CompaniesController(ICompanyAdminService companyAdminService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await companyAdminService.GetAllAsync(cancellationToken);
        return Ok(result);
    }
}
