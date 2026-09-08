using System.Security.Claims;
using Erp.Api.Authorization;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Inventory;

/// <summary>
/// Stock counts, total or partial. Zeroing the stock is not a separate operation: it is a count
/// opened with every line at zero.
/// </summary>
[ApiController]
[Route("api/inventory-counts")]
[Authorize]
[Produces("application/json")]
public sealed class InventoryCountsController(IInventoryCountService countService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<InventoryCountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<InventoryCountDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await countService.GetAllAsync(companyId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<InventoryCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryCountDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await countService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Opens a count over the stock in scope, taking a picture of what the system holds.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InventoryCountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryCountDto>> Open(
        [FromBody] OpenInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var opened = await countService.OpenAsync(request, GetCurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = opened.Id }, opened);
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

    /// <summary>
    /// Adds a product to an open sheet — something found that the system had never heard of, or the
    /// opening stock of a warehouse it believes is empty.
    /// </summary>
    [HttpPost("{id:guid}/lines")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InventoryCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryCountDto>> AddLine(
        Guid id,
        [FromBody] AddCountLineRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        try
        {
            var result = await countService.AddLineAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Already on the sheet, or the count is closed. Both are conflicts: the request was
            // well formed, the sheet had moved on.
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Records what was found, for one or more lines.</summary>
    [HttpPut("{id:guid}/lines")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InventoryCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryCountDto>> SetCounted(
        Guid id,
        [FromBody] IReadOnlyList<CountedLineRequest> lines,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await countService.SetCountedAsync(id, lines, cancellationToken);
            return result is null ? NotFound() : Ok(result);
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

    /// <summary>
    /// Closes the count and writes the differences to the ledger. Measured against the balances as
    /// they stand now, so movements made while the count was open are not undone.
    /// </summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InventoryCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryCountDto>> Close(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await countService.CloseAsync(id, GetCurrentUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
