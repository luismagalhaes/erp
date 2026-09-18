using System.Security.Claims;
using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// Orders placed with suppliers. Not fiscal documents: nobody signs them and they carry no ATCUD,
/// so unlike everything in Sales they can be corrected in place — until goods start arriving.
/// </summary>
/// <remarks>
/// The supplier and the warehouse belong to Core, so the host reads them and hands them over. The
/// Purchasing module never depends on Core, the same way Sales does not.
/// </remarks>
[ApiController]
[Route("api/purchase-orders")]
[Authorize]
[Produces("application/json")]
[ScopedEntity(typeof(PurchaseOrder))]
public sealed class PurchaseOrdersController(
    IPurchaseOrderService orderService,
    ISupplierService supplierService,
    IWarehouseService warehouseService,
    ILogger<PurchaseOrdersController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<PurchaseOrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] bool openOnly = false)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await orderService.GetAllAsync(companyId, supplierId, openOnly, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<PurchaseOrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<PurchaseOrderListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<PurchaseOrderListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(orderService.Query(companyId), options));
    }

    /// <summary>What suppliers still owe, line by line, across every open order.</summary>
    [HttpGet("pending")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<PendingOrderLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PendingOrderLineDto>>> GetPending(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await orderService.GetPendingLinesAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await orderService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseOrderDto>> Create(
        [FromBody] CreatePurchaseOrderApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var supplier = await supplierService.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        var warehouseError = await ValidateWarehouseAsync(request.CompanyId, request.WarehouseId, cancellationToken);
        if (warehouseError is not null)
            return warehouseError;

        try
        {
            var created = await orderService.CreateAsync(
                new CreatePurchaseOrderRequest(
                    request.CompanyId,
                    request.SupplierId,
                    new PurchaseOrderSupplierDto(
                        supplier.Code,
                        supplier.Name,
                        supplier.TaxId,
                        supplier.Address,
                        supplier.PostalCode,
                        supplier.City,
                        supplier.Country),
                    request.OrderDate,
                    request.WarehouseId,
                    request.Lines,
                    request.ExpectedDate,
                    request.Notes,
                    request.Place),
                GetCurrentUserId(),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(PurchaseOrdersController), nameof(Create));
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Rewrites an order. Refused once goods have started arriving against it.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseOrderDto>> Update(
        Guid id,
        [FromBody] UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var existing = await orderService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound();

        var warehouseError = await ValidateWarehouseAsync(existing.CompanyId, request.WarehouseId, cancellationToken);
        if (warehouseError is not null)
            return warehouseError;

        return await RunAsync(() => orderService.UpdateAsync(id, request, cancellationToken));
    }

    /// <summary>Sends a draft to the supplier.</summary>
    [HttpPost("{id:guid}/place")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PurchaseOrderDto>> Place(Guid id, CancellationToken cancellationToken) =>
        RunAsync(() => orderService.PlaceAsync(id, cancellationToken));

    /// <summary>Closes an order that will not be completed, writing off what is still owed.</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PurchaseOrderDto>> Close(
        Guid id,
        [FromBody] ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(() => orderService.CloseAsync(id, request, cancellationToken));

    /// <summary>Calls an order off. Only possible while nothing has arrived.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseOrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<ActionResult<PurchaseOrderDto>> Cancel(
        Guid id,
        [FromBody] ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken) =>
        RunAsync(() => orderService.CancelAsync(id, request, cancellationToken));

    /// <summary>
    /// Runs an operation that returns null for an unknown order, mapping the domain's refusals to
    /// status codes. An <see cref="InvalidOperationException"/> here always means the order is in a
    /// state that does not allow what was asked, which is a conflict and not a bad request.
    /// </summary>
    private async Task<ActionResult<PurchaseOrderDto>> RunAsync(Func<Task<PurchaseOrderDto?>> operation)
    {
        try
        {
            var result = await operation();
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(PurchaseOrdersController), nameof(RunAsync));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(PurchaseOrdersController), nameof(RunAsync));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>The warehouse belongs to Core, and has to be one of this company's.</summary>
    private async Task<ActionResult?> ValidateWarehouseAsync(
        Guid companyId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId == Guid.Empty)
            return BadRequest(new { error = "warehouseId is required." });

        var warehouse = await warehouseService.GetByIdAsync(warehouseId, cancellationToken);

        if (warehouse is null || warehouse.CompanyId != companyId)
            return NotFound(new { error = "Warehouse not found for this company." });

        return null;
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
