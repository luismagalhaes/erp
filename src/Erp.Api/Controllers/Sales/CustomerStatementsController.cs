using Erp.Api.Services;
using Erp.Common.Statements;
using Erp.Core.Infrastructure.Application;
using Erp.Sales.Infrastructure.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Sales;

/// <summary>
/// Customer current account statements. The customer lives in Core and the documents in Sales, so
/// the host reads one and hands the tax id to the other — the documents know the customer only by it.
/// </summary>
[ApiController]
[Route("api/customer-statements")]
[Authorize]
[Produces("application/json")]
public sealed class CustomerStatementsController(
    ICustomerStatementService statementService,
    ICustomerService customerService,
    ILogger<CustomerStatementsController> logger) : ControllerBase
{
    /// <summary>
    /// The statement of one customer. Without dates it covers everything; with a start date, what
    /// came before it is summed into the opening balance.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<AccountStatement>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStatement>> Get(
        [FromQuery] Guid companyId,
        [FromQuery] Guid customerId,
        CancellationToken cancellationToken,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        var customer = await customerService.GetByIdAsync(customerId, cancellationToken);
        if (customer is null)
            return NotFound(new { error = "Customer not found." });

        try
        {
            return Ok(await statementService.GetAsync(
                companyId, customer.TaxId, customer.Name, startDate, endDate, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(CustomerStatementsController), nameof(Get));
            return BadRequest(new { error = ex.Message });
        }
    }
}
