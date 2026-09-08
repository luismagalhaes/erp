using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>Supplier master file, exported as the SAF-T Supplier table.</summary>
[ApiController]
[Route("api/suppliers")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class SuppliersController(ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PartnerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await supplierService.GetAllAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [ProducesResponseType<ODataCollection<PartnerDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<PartnerDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<PartnerDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(supplierService.Query(companyId), options));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PartnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await supplierService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PartnerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PartnerDto>> Create(
        [FromBody] CreatePartnerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await supplierService.CreateAsync(request, cancellationToken);
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
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PartnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerDto>> Update(
        Guid id,
        [FromBody] UpdatePartnerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await supplierService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
