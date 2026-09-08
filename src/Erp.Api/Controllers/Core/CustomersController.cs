using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Core;

/// <summary>Customer master file, exported as the SAF-T Customer table.</summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PartnerDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PartnerDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await customerService.GetAllAsync(companyId, cancellationToken));
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

        return Ok(ODataQueryExecutor.Execute(customerService.Query(companyId), options));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PartnerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PartnerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await customerService.GetByIdAsync(id, cancellationToken);
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
            var created = await customerService.CreateAsync(request, cancellationToken);
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
            var updated = await customerService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

