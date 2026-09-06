using System.Security.Claims;
using Erp.Api.Authorization;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Sales;

/// <summary>Issuing and consultation of certified sales documents.</summary>
[ApiController]
[Route("api/invoices")]
[Authorize]
[Produces("application/json")]
public sealed class InvoicesController(ISalesDocumentService salesDocumentService) : ControllerBase
{
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
            var issued = await salesDocumentService.IssueAsync(request, GetCurrentUserId(), cancellationToken);
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
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
