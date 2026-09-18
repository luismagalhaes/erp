using Erp.Api.Services;
using Erp.Common.Statements;
using Erp.Core.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// Supplier current account statements. The supplier is read from Core; the documents and payments
/// that make up the account are Purchasing's.
/// </summary>
[ApiController]
[Route("api/supplier-statements")]
[Authorize]
[Produces("application/json")]
public sealed class SupplierStatementsController(
    ISupplierStatementService statementService,
    ISupplierService supplierService,
    ILogger<SupplierStatementsController> logger) : ControllerBase
{
    /// <summary>
    /// The statement of one supplier. Without dates it covers everything; with a start date, what
    /// came before it is summed into the opening balance.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<AccountStatement>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStatement>> Get(
        [FromQuery] Guid companyId,
        [FromQuery] Guid supplierId,
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var supplier = await supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
            return NotFound(new { error = "Supplier not found." });

        try
        {
            return Ok(await statementService.GetAsync(
                companyId, supplierId, supplier.Name, supplier.TaxId, startDate, endDate, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(SupplierStatementsController), nameof(Get));
            return BadRequest(new { error = ex.Message });
        }
    }
}
