using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Core;

/// <summary>
/// Warehouses. Master data, because stock is kept per warehouse and more than one module needs
/// to name them.
/// </summary>
[ApiController]
[Route("api/warehouses")]
[Authorize(Policy = Policies.Read)]
[Produces("application/json")]
public sealed class WarehousesController(IWarehouseService warehouseService, ILogger<WarehousesController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await warehouseService.GetAllAsync(companyId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await warehouseService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseDto>> Create(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await warehouseService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(WarehousesController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(WarehousesController), nameof(Create));
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<WarehouseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseDto>> Update(
        Guid id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await warehouseService.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(WarehousesController), nameof(Update));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(WarehousesController), nameof(Update));
            return Conflict(new { error = ex.Message });
        }
    }
}
