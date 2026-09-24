using Erp.Api.Security;
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
    ISubscriptionPlanService subscriptionPlanService,
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

    /// <summary>
    /// Every subscription the company has ever had or is scheduled for — expired, currently
    /// running, or starting later — for the company editor's subscription tab.
    /// </summary>
    [HttpGet("{companyId:guid}/all")]
    [ProducesResponseType<IReadOnlyList<CompanySubscriptionDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanySubscriptionDto>>> GetAllForCompany(Guid companyId, CancellationToken cancellationToken) =>
        Ok(await companySubscriptionService.GetAllForCompanyAsync(companyId, cancellationToken));

    /// <summary>
    /// Puts a company on a package, adding a new row rather than replacing whatever it already
    /// has — so a company can be on a free plan today and already have an annual one scheduled to
    /// start later. Exempted from <see cref="RequireActiveSubscriptionFilter"/>: this is the very
    /// endpoint a company with no subscription — or an expired one — needs to be able to reach.
    /// </summary>
    [HttpPut("{companyId:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [AllowWithoutSubscription]
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

    /// <summary>
    /// A company putting itself on a plan — no admin involved. This is what a caller with an
    /// expired subscription, or none at all, uses to get one back: exempted from
    /// <see cref="RequireActiveSubscriptionFilter"/> for the same reason as <see cref="Assign"/>,
    /// but authorized by plain company membership rather than <see cref="Policies.Admin"/>, since
    /// that is the whole point of it being self-service.
    /// </summary>
    [HttpPost("{companyId:guid}/self-service")]
    [AllowWithoutSubscription]
    [ProducesResponseType<CompanySubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanySubscriptionDto>> SelfServiceSubscribe(
        Guid companyId,
        [FromBody] SelfServiceSubscribeRequest request,
        CancellationToken cancellationToken)
    {
        var plan = await subscriptionPlanService.GetByIdAsync(request.PlanId, cancellationToken);

        // Self-service only ever offers what sign-up itself offers — a retired plan is not
        // something a company can put itself back on, no matter how it got the id.
        if (plan is null || !plan.IsActive)
            return BadRequest(new { error = "This plan is not available." });

        try
        {
            var assigned = await companySubscriptionService.AssignAsync(
                companyId,
                new AssignSubscriptionRequest(request.PlanId),
                cancellationToken);
            return Ok(assigned);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CompanySubscriptionsController), nameof(SelfServiceSubscribe));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(CompanySubscriptionsController), nameof(SelfServiceSubscribe));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Removes one of the company's subscription rows entirely \u2014 for a package assigned by
    /// mistake, rather than one that simply expired.
    /// </summary>
    [HttpDelete("{companyId:guid}/{subscriptionId:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid companyId, Guid subscriptionId, CancellationToken cancellationToken)
    {
        var removed = await companySubscriptionService.RemoveAsync(companyId, subscriptionId, cancellationToken);
        return removed ? NoContent() : NotFound();
    }
}
