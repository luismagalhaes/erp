using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// The subscription packages on offer. Not company scoped — the same packages apply to everyone,
/// so there is no companyId here; only a SuperAdmin changes them, but anyone signed in can read
/// them, since the sign-up page has to show them before the user has a company at all.
/// </summary>
[ApiController]
[Route("api/subscription-plans")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class SubscriptionPlansController(
    ISubscriptionPlanService subscriptionPlanService,
    ILogger<SubscriptionPlansController> logger) : ControllerBase
{
    /// <param name="activeOnly">True for the sign-up page, which must not offer a retired package.</param>
    /// <param name="cancellationToken"></param>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SubscriptionPlanDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanDto>>> GetAll(
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken)
    {
        var result = await subscriptionPlanService.GetAllAsync(activeOnly, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The listing the backoffice data grid calls. Filtering, sorting and paging travel as OData
    /// options and are applied by the database.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType<ODataCollection<SubscriptionPlanDto>>(StatusCodes.Status200OK)]
    public ActionResult<ODataCollection<SubscriptionPlanDto>> Query(ODataQueryOptions<SubscriptionPlanDto> options) =>
        Ok(ODataQueryExecutor.Execute(subscriptionPlanService.Query(), options));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SubscriptionPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionPlanDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await subscriptionPlanService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SubscriptionPlanDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionPlanDto>> Create(
        [FromBody] CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await subscriptionPlanService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(SubscriptionPlansController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SubscriptionPlansController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Deactivating a package is an update — a package a company is on is never deleted.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SubscriptionPlanDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubscriptionPlanDto>> Update(
        Guid id,
        [FromBody] UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await subscriptionPlanService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(SubscriptionPlansController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(SubscriptionPlansController), nameof(Update));
            return Conflict(new { error = ex.Message });
        }
    }
}
