using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// Which company is on which package. The listing is the SuperAdmin backoffice's — a regular user
/// only ever sees their own company's row, which the tenant filter takes care of on its own.
/// Reassigning a package is the "they bought the annual one" action: there is no payment gateway
/// behind this, so it is recorded rather than charged.
/// </summary>
[ApiController]
[Route("api/company-subscriptions")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CompanySubscriptionsController(
    ICompanySubscriptionService companySubscriptionService,
    ILogger<CompanySubscriptionsController> logger) : ControllerBase
{
    /// <summary>
    /// The listing the backoffice data grid calls. Filtering, sorting and paging travel as OData
    /// options and are applied by the database.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType<ODataCollection<CompanySubscriptionDto>>(StatusCodes.Status200OK)]
    public ActionResult<ODataCollection<CompanySubscriptionDto>> Query(ODataQueryOptions<CompanySubscriptionDto> options) =>
        Ok(ODataQueryExecutor.Execute(companySubscriptionService.Query(), options));

    /// <summary>What a company is subscribed to, or 404 when it has never been put on a package.</summary>
    [HttpGet("{companyId:guid}")]
    [ProducesResponseType<CompanySubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanySubscriptionDto>> GetForCompany(Guid companyId, CancellationToken cancellationToken)
    {
        var result = await companySubscriptionService.GetForCompanyAsync(companyId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Moves a company onto a package, replacing whatever it was on.</summary>
    [HttpPut("{companyId:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<CompanySubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanySubscriptionDto>> Assign(
        Guid companyId,
        [FromBody] AssignSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var assigned = await companySubscriptionService.AssignAsync(companyId, request, cancellationToken);
            return Ok(assigned);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompanySubscriptionsController), nameof(Assign));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompanySubscriptionsController), nameof(Assign));
            return Conflict(new { error = ex.Message });
        }
    }
}
