using System.Security.Claims;
using Erp.Api.Services;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// Goods going back to suppliers. The mirror of a receipt: stock leaves the warehouse at the cost
/// it came in at, and only what is still in hand can go.
/// </summary>
/// <remarks>
/// This records the movement, not the transport. Goods that physically travel back also need a
/// transport document, and <b>that one is fiscal and ours</b>: a delivery note of type <c>GD</c>,
/// issued through <c>/api/stock-movements</c>.
/// </remarks>
[ApiController]
[Route("api/supplier-returns")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierReturnsController(
    ISupplierReturnService returnService,
    ISupplierService supplierService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<SupplierReturnListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SupplierReturnListItemDto>>> GetAll(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await returnService.GetAllAsync(companyId, supplierId, cancellationToken));
    }

    /// <summary>What is still in hand from each receipt, and so could go back.</summary>
    [HttpGet("returnable")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<ReturnableReceiptLineDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ReturnableReceiptLineDto>>> GetReturnable(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken,
        [FromQuery] Guid? supplierId = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await returnService.GetReturnableLinesAsync(companyId, supplierId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<SupplierReturnDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierReturnDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await returnService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<SupplierReturnDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierReturnDto>> Create(
        [FromBody] CreateSupplierReturnApiRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A request body is required." });

        var supplier = await supplierService.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        try
        {
            var created = await returnService.CreateAsync(
                new CreateSupplierReturnRequest(
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
                    request.ReturnDate,
                    request.Reason,
                    request.Lines,
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
            // Sending back more than is still in hand. A conflict: the request was well formed,
            // the warehouse had moved on.
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Undoes a return: the stock comes back in and the goods are ours again.</summary>
    [HttpPost("{id:guid}/void")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<SupplierReturnDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierReturnDto>> Void(
        Guid id,
        [FromBody] VoidGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A reason is required." });

        try
        {
            var result = await returnService.VoidAsync(id, request.Reason, GetCurrentUserId(), cancellationToken);
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
