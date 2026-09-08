using System.Security.Claims;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// Supplier invoices, written into our books. <b>Not documents of ours</b>: the supplier numbered,
/// signed and communicated them — we are only recording them, for the input VAT and the accounts.
/// </summary>
/// <remarks>
/// Two rules matter here. The same document from the same supplier may only be recorded once, or
/// the VAT is deducted twice. And a line that comes from a goods receipt moves no stock, because
/// the receipt already moved it.
/// </remarks>
[ApiController]
[Route("api/purchase-invoices")]
[Authorize]
[Produces("application/json")]
public sealed class PurchaseInvoicesController(
    IPurchaseInvoiceService invoiceService,
    ISupplierService supplierService,
    IWarehouseService warehouseService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<PurchaseInvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PurchaseInvoiceListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await invoiceService.GetAllAsync(companyId, supplierId, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<PurchaseInvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<PurchaseInvoiceListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<PurchaseInvoiceListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(invoiceService.Query(companyId), options));
    }

    /// <summary>What has been received but not yet invoiced. Recording an invoice starts here.</summary>
    [HttpGet("uninvoiced-receipts")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<UninvoicedReceiptLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<UninvoicedReceiptLineDto>>> GetUninvoicedReceipts(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await invoiceService.GetUninvoicedReceiptLinesAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<PurchaseInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PurchaseInvoiceDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await invoiceService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseInvoiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseInvoiceDto>> Record(
        [FromBody] RecordPurchaseInvoiceApiRequest request,
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
            var created = await invoiceService.RecordAsync(
                new RecordPurchaseInvoiceRequest(
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
                    request.DocumentType,
                    request.SupplierDocumentNumber,
                    request.SupplierDocumentDate,
                    request.ReceivedDate,
                    request.Lines,
                    request.WarehouseId,
                    request.SupplierAtcud,
                    request.DueDate,
                    request.ReverseCharge,
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
            // A duplicate, or more invoiced than was received. Both are conflicts: the request was
            // well formed, the books had moved on.
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Corrects a record. Refused on one that brought goods into stock.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseInvoiceDto>> Update(
        Guid id,
        [FromBody] UpdatePurchaseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var existing = await invoiceService.GetByIdAsync(id, cancellationToken);
        if (existing is null)
            return NotFound();

        var warehouseError = await ValidateWarehouseAsync(existing.CompanyId, request.WarehouseId, cancellationToken);
        if (warehouseError is not null)
            return warehouseError;

        try
        {
            var result = await invoiceService.UpdateAsync(id, request, cancellationToken);
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

    /// <summary>Strikes the record out, reversing any stock it brought in.</summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PurchaseInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PurchaseInvoiceDto>> Void(
        Guid id,
        [FromBody] VoidPurchaseInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A reason is required." });

        try
        {
            var result = await invoiceService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>The warehouse is optional here — a services invoice needs none — but must be ours.</summary>
    private async Task<ActionResult?> ValidateWarehouseAsync(
        Guid companyId,
        Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        if (warehouseId is not { } id || id == Guid.Empty)
            return null;

        var warehouse = await warehouseService.GetByIdAsync(id, cancellationToken);

        if (warehouse is null || warehouse.CompanyId != companyId)
            return NotFound(new { error = "Warehouse not found for this company." });

        return null;
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
