using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// Environmental fees ("Ecovalor") an article can carry — the fee on batteries, on lubricating
/// oils, and other waste streams under DL 152-D/2017. Creating one also creates the pseudo-product
/// it is invoiced as, which is why writes go through <see cref="IEcoFeeTypeService"/> rather than
/// the plain product catalogue.
/// </summary>
[ApiController]
[Route("api/eco-fee-types")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
[ScopedEntity(typeof(EcoFeeType))]
public sealed class EcoFeeTypesController(IEcoFeeTypeService ecoFeeTypeService, ILogger<EcoFeeTypesController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<EcoFeeTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EcoFeeTypeDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await ecoFeeTypeService.GetAllAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType<ODataCollection<EcoFeeTypeDto>>(StatusCodes.Status200OK)]
    public ActionResult<ODataCollection<EcoFeeTypeDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<EcoFeeTypeDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(ecoFeeTypeService.Query(companyId), options));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<EcoFeeTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EcoFeeTypeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await ecoFeeTypeService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<EcoFeeTypeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EcoFeeTypeDto>> Create(
        [FromBody] CreateEcoFeeTypeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return BadRequest(new { error = "A request body is required." });

            var created = await ecoFeeTypeService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(EcoFeeTypesController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(EcoFeeTypesController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<EcoFeeTypeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EcoFeeTypeDto>> Update(
        Guid id,
        [FromBody] UpdateEcoFeeTypeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await ecoFeeTypeService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(EcoFeeTypesController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
    }
}
