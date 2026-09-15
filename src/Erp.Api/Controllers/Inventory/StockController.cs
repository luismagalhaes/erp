using System.Security.Claims;
using Erp.Api.Services;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.Api.Controllers.Inventory;

/// <summary>Stock balances, the ledger behind them, and the check that the two agree.</summary>
[ApiController]
[Route("api/stock")]
[Authorize]
[Produces("application/json")]
public sealed class StockController(IStockService stockService, ILogger<StockController> logger) : ControllerBase
{
    /// <summary>Current stock, optionally narrowed to one warehouse or one product.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<StockBalanceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StockBalanceDto>>> GetBalances(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] string? productCode,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await stockService.GetBalancesAsync(companyId, warehouseId, productCode, cancellationToken));
    }

    /// <summary>The movements behind one balance, oldest first, with the balance after each.</summary>
    [HttpGet("ledger")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<IReadOnlyList<StockLedgerEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<StockLedgerEntryDto>>> GetLedger(
        [FromQuery] Guid companyId,
        [FromQuery] Guid warehouseId,
        [FromQuery] string productCode,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || warehouseId == Guid.Empty || string.IsNullOrWhiteSpace(productCode))
            return BadRequest(new { error = "companyId, warehouseId and productCode are required." });

        return Ok(await stockService.GetLedgerAsync(companyId, warehouseId, productCode, cancellationToken));
    }

    /// <summary>
    /// Recomputes the balances from the ledger and reports where the two disagree. Leave the
    /// product out to check the whole company.
    /// </summary>
    [HttpGet("check")]
    [Authorize(Policy = Policies.Read)]
    [ProducesResponseType<StockCheckResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockCheckResultDto>> Check(
        [FromQuery] Guid companyId,
        [FromQuery] string? productCode,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
            return BadRequest(new { error = "companyId is required." });

        return Ok(await stockService.CheckAsync(companyId, productCode, cancellationToken));
    }

    /// <summary>Records a manual correction as a new ledger entry.</summary>
    [HttpPost("adjustments")]
    [Authorize(Policy = Policies.Write)]
    [ProducesResponseType<StockBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StockBalanceDto>> Adjust(
        [FromBody] AdjustStockRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await stockService.AdjustAsync(request, GetCurrentUserId(), cancellationToken));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "{Controller}.{Method} rejected an invalid request.", nameof(StockController), nameof(Adjust));
            return BadRequest(new { error = ex.Message });
        }
    }

    private string? GetCurrentUserId() =>
        User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
