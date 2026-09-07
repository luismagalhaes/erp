using Erp.Inventory.Infrastructure.Contracts;

namespace Erp.Inventory.Infrastructure.Application;

public interface IStockService
{
    /// <summary>Current stock, optionally narrowed to one warehouse or one product.</summary>
    Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>The movements behind a balance, oldest first, with the balance after each one.</summary>
    Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes the balances from the ledger and compares them with the ones on record.
    /// </summary>
    /// <param name="productCode">Null checks every product of the company.</param>
    Task<StockCheckResultDto> CheckAsync(
        Guid companyId,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a manual correction, which is a new ledger entry and never an edit.</summary>
    Task<StockBalanceDto> AdjustAsync(
        AdjustStockRequest request,
        string? userId,
        CancellationToken cancellationToken = default);
}
