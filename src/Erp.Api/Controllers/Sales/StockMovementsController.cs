using System.Security.Claims;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Sales;

/// <summary>
/// Goods movement documents, exported under SAF-T MovementOfGoods. They are issued, numbered and
/// signed like invoices, and must reach the tax authority before the transport starts.
/// </summary>
[ApiController]
[Route("api/stock-movements")]
[Authorize]
[Produces("application/json")]
public sealed class StockMovementsController(
    IStockMovementService stockMovementService,
    IWarehouseService warehouseService) : ControllerBase
{
    /// <summary>
    /// Falls back to the company's default warehouse when the caller names none. The warehouse
    /// file belongs to Core, so this is the host's job rather than the Sales module's.
    /// </summary>
    private async Task<Guid?> ResolveWarehouseAsync(
        Guid companyId,
        Guid? requested,
        CancellationToken cancellationToken)
    {
        if (requested is { } warehouseId && warehouseId != Guid.Empty)
            return warehouseId;

        var warehouses = await warehouseService.GetAllAsync(companyId, cancellationToken);

        return warehouses.FirstOrDefault(x => x.IsDefault)?.Id;
    }

    /// <summary>Lists the movements issued by a company, most recent first.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<StockMovementListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StockMovementListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await stockMovementService.GetAllAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<StockMovementListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<StockMovementListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<StockMovementListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(stockMovementService.Query(companyId), options));
    }

    /// <summary>Gets a movement with its lines, transport data, signature and QR code message.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<StockMovementDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await stockMovementService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Issues a goods movement: takes the next number in the series, signs it against the
    /// previous document of that series and persists everything in one transaction.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<StockMovementDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDetailDto>> Issue(
        [FromBody] CreateStockMovementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var withWarehouse = request with
            {
                WarehouseId = await ResolveWarehouseAsync(request.CompanyId, request.WarehouseId, cancellationToken)
            };

            var issued = await stockMovementService.IssueAsync(withWarehouse, GetCurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = issued.Id }, issued);
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
    /// Records the code the tax authority returned for this document. Without it the goods
    /// cannot legally start moving.
    /// </summary>
    [HttpPost("{id:guid}/communicate")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<StockMovementDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDetailDto>> Communicate(
        Guid id,
        [FromBody] CommunicateStockMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.AtDocCodeId))
            return BadRequest(new { error = "The code returned by the tax authority is required." });

        try
        {
            var result = await stockMovementService.CommunicateAsync(id, request.AtDocCodeId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Voids a movement. The original record is preserved: this writes a status change, it does
    /// not alter or remove the document.
    /// </summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<StockMovementDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementDetailDto>> Void(
        Guid id,
        [FromBody] VoidStockMovementRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { error = "A reason is required to void a document." });

        try
        {
            var result = await stockMovementService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
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
