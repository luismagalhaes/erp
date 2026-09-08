using System.Security.Claims;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// Goods arriving from suppliers. This is where a purchase moves stock: the receipt brings the
/// goods into the warehouse and credits the order lines it came against, in one transaction.
/// </summary>
/// <remarks>
/// Not a fiscal document of ours — the delivery note that came with the lorry is the supplier's,
/// and its number is recorded as a reference, never adopted as ours.
/// </remarks>
[ApiController]
[Route("api/goods-receipts")]
[Authorize]
[Produces("application/json")]
public sealed class GoodsReceiptsController(
    IGoodsReceiptService receiptService,
    ISupplierService supplierService,
    IWarehouseService warehouseService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<GoodsReceiptListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<GoodsReceiptListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await receiptService.GetAllAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<GoodsReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GoodsReceiptDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await receiptService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<GoodsReceiptDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GoodsReceiptDto>> Create(
        [FromBody] CreateGoodsReceiptApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var supplier = await supplierService.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        if (request.WarehouseId == Guid.Empty)
            return BadRequest(new { error = "warehouseId is required." });

        var warehouse = await warehouseService.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse is null || warehouse.CompanyId != request.CompanyId)
            return NotFound(new { error = "Warehouse not found for this company." });

        try
        {
            var created = await receiptService.CreateAsync(
                new CreateGoodsReceiptRequest(
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
                    request.ReceiptDate,
                    request.WarehouseId,
                    request.Lines,
                    request.SupplierDocumentNumber,
                    request.SupplierDocumentDate,
                    request.Notes),
                GetCurrentUserId(),
                cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Receiving more than the order still owes. A conflict rather than a bad request: the
            // request was well formed, the order had moved on.
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Undoes a receipt: takes the stock back out and gives the order its quantity back.</summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<GoodsReceiptDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GoodsReceiptDto>> Void(
        Guid id,
        [FromBody] VoidGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A reason is required." });

        try
        {
            var result = await receiptService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
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
