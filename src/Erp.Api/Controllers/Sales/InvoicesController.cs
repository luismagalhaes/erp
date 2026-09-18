using System.Security.Claims;
using Erp.Api.Security;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Sales;

/// <summary>Issuing and consultation of certified sales documents.</summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
[Produces("application/json")]
[ScopedEntity(typeof(SalesDocument))]
public sealed class InvoicesController(
    ISalesDocumentService salesDocumentService,
    IWarehouseService warehouseService,
    ILogger<InvoicesController> logger) : ControllerBase
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

    /// <summary>Lists the documents issued by a company, most recent first.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<InvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<InvoiceListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var result = await salesDocumentService.GetAllAsync(companyId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<InvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<InvoiceListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<InvoiceListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(salesDocumentService.Query(companyId), options));
    }

    /// <summary>
    /// Goods movement lines with quantity still to invoice, so an invoice can be built from the
    /// delivery notes instead of being typed again.
    /// </summary>
    [HttpGet("pending-movements")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<PendingMovementLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PendingMovementLineDto>>> GetPendingMovements(
        [FromQuery] Guid companyId,
        [FromQuery] string? partyTaxId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await salesDocumentService.GetPendingMovementLinesAsync(companyId, partyTaxId, cancellationToken));
    }

    /// <summary>Gets a document with its lines, tax totals, signature and QR code message.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await salesDocumentService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Issues a document: takes the next number in the series, signs it against the previous
    /// document of that series and persists everything in one transaction.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetailDto>> Issue(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var withWarehouse = request with
            {
                WarehouseId = await ResolveWarehouseAsync(request.CompanyId, request.WarehouseId, cancellationToken)
            };

            var issued = await salesDocumentService.IssueAsync(withWarehouse, GetCurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = issued.Id }, issued);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(InvoicesController), nameof(Issue));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(InvoicesController), nameof(Issue));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Voids a document. The original record is preserved: this writes a status change, it does
    /// not alter or remove the document.
    /// </summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<InvoiceDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvoiceDetailDto>> Void(
        Guid id,
        [FromBody] VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { error = "A reason is required to void a document." });

        try
        {
            var result = await salesDocumentService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(InvoicesController), nameof(Void));
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
