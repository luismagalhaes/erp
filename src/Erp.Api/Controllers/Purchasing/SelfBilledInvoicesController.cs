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
/// Invoices we issue <b>in a supplier's name</b>, under article 36.º n.º 11 of the CIVA. The only
/// certified document this module produces.
/// </summary>
/// <remarks>
/// Everything else under <c>api/purchase-*</c> records what a supplier sent us. These are the other
/// way round: we issue them, so they are numbered from a communicated series, signed into a chain,
/// never edited, and voided only by appending a status change.
/// </remarks>
[ApiController]
[Route("api/self-billed-invoices")]
[Authorize]
[Produces("application/json")]
public sealed class SelfBilledInvoicesController(
    ISelfBilledInvoiceService invoiceService,
    ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<SelfBilledInvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SelfBilledInvoiceListItemDto>>> GetAll(
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
    [ProducesResponseType<ODataCollection<SelfBilledInvoiceListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<SelfBilledInvoiceListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<SelfBilledInvoiceListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(invoiceService.Query(companyId), options));
    }

    /// <summary>What has been received but not yet self-billed. Issuing normally starts here.</summary>
    [HttpGet("unbilled-receipts")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<UnbilledReceiptLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<UnbilledReceiptLineDto>>> GetUnbilledReceipts(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await invoiceService.GetUnbilledReceiptLinesAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<SelfBilledInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SelfBilledInvoiceDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await invoiceService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<SelfBilledInvoiceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SelfBilledInvoiceDto>> Issue(
        [FromBody] IssueSelfBilledInvoiceApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var supplier = await supplierService.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        // The supplier's tax id is the header of the SAF-T file this document ends up in. Issuing
        // without one would produce a document with no file to belong to.
        if (string.IsNullOrWhiteSpace(supplier.TaxId))
            return BadRequest(new { error = "The supplier has no tax id, which self-billing requires." });

        try
        {
            var created = await invoiceService.IssueAsync(
                new IssueSelfBilledInvoiceRequest(
                    request.CompanyId,
                    request.SupplierId,
                    request.SeriesId,
                    request.IssueDate,
                    new PurchaseOrderSupplierDto(
                        supplier.Code,
                        supplier.Name,
                        supplier.TaxId,
                        supplier.Address,
                        supplier.PostalCode,
                        supplier.City,
                        supplier.Country),
                    request.Lines,
                    request.SupplierAgreementReference),
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
            // A series with no validation code, or more billed than was received. Both are
            // conflicts: the request was well formed, the books had moved on.
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Records the supplier's acceptance, which article 36.º n.º 11 requires of each document.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<SelfBilledInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SelfBilledInvoiceDto>> Accept(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await invoiceService.AcceptAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Voids the document. The record is not touched: the document stays in the SAF-T with status
    /// "A", as an issued document must.
    /// </summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<SelfBilledInvoiceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SelfBilledInvoiceDto>> Void(
        Guid id,
        [FromBody] VoidSelfBilledInvoiceRequest request,
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

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
