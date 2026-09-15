using System.Security.Claims;
using Erp.Api.Services;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace Erp.Api.Controllers.Sales;

/// <summary>
/// Receipts, exported under SAF-T Payments. A receipt is issued, numbered and signed like an
/// invoice, and says which invoices it settles and by how much.
/// </summary>
[ApiController]
[Route("api/payments")]
[Authorize]
[Produces("application/json")]
public sealed class PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger) : ControllerBase
{
    /// <summary>Lists the receipts issued by a company, most recent first.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<PaymentListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PaymentListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await paymentService.GetAllAsync(companyId, cancellationToken));
    }

    /// <summary>
    /// The listing the data grid calls. Filtering, sorting and paging travel as OData options and
    /// are applied by the database; the company stays outside the query, as a tenancy boundary the
    /// caller cannot widen.
    /// </summary>
    [HttpGet("odata")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<ODataCollection<PaymentListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ODataCollection<PaymentListItemDto>> Query(
        [FromQuery] Guid companyId,
        ODataQueryOptions<PaymentListItemDto> options)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(ODataQueryExecutor.Execute(paymentService.Query(companyId), options));
    }

    /// <summary>
    /// Invoices with money still owed, so a receipt can be built from them. Optionally narrowed
    /// to one customer's tax id.
    /// </summary>
    [HttpGet("outstanding-invoices")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<OutstandingInvoiceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<OutstandingInvoiceDto>>> GetOutstandingInvoices(
        [FromQuery] Guid companyId,
        [FromQuery] string? customerTaxId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await paymentService.GetOutstandingInvoicesAsync(companyId, customerTaxId, cancellationToken));
    }

    /// <summary>Gets a receipt with its lines, payment methods, signature and QR code message.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<PaymentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await paymentService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Issues a receipt: takes the next number in the series, signs it against the previous
    /// receipt of that series and persists everything in one transaction.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PaymentDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentDetailDto>> Issue(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var issued = await paymentService.IssueAsync(request, GetCurrentUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = issued.Id }, issued);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(PaymentsController), nameof(Issue));
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(PaymentsController), nameof(Issue));
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Voids a receipt. The original record is preserved: this writes a status change, and the
    /// invoices it settled go back to being owed.
    /// </summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<PaymentDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentDetailDto>> Void(
        Guid id,
        [FromBody] VoidPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
            return BadRequest(new { error = "A reason is required to void a document." });

        try
        {
            var result = await paymentService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} could not complete due to a conflict.", nameof(PaymentsController), nameof(Void));
            return Conflict(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
