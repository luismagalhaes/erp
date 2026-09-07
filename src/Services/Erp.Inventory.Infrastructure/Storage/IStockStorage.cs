using Erp.Inventory.Domain;

namespace Erp.Inventory.Infrastructure.Storage;

public interface IStockStorage
{
    /// <param name="warehouseId">Null returns every warehouse of the company.</param>
    Task<IReadOnlyList<StockBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes an update lock on the balance row so two movements of the same product in the same
    /// warehouse queue up instead of overwriting one another. Returns null when there is none yet.
    /// </summary>
    Task<StockBalance?> GetBalanceForUpdateAsync(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        CancellationToken cancellationToken = default);

    Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken = default);

    /// <param name="productCode">Null returns the ledger of every product.</param>
    Task<IReadOnlyList<StockLedgerEntry>> GetEntriesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The quantity each product and warehouse adds up to in the ledger. This is what the stock
    /// check compares the recorded balances against, so it must be read from the entries and never
    /// from the balances.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid WarehouseId, string ProductCode), (decimal Quantity, int EntryCount)>>
        SumLedgerAsync(Guid companyId, string? productCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The stock each product held on a given date, summed from the ledger. The inventory file
    /// reports the stock at the end of the period, which is not the same as the stock today.
    /// </summary>
    Task<IReadOnlyDictionary<string, (decimal Quantity, string Description)>> SumLedgerAsAtAsync(
        Guid companyId,
        DateOnly asAt,
        CancellationToken cancellationToken = default);

    /// <summary>True when that document line has already moved stock.</summary>
    Task<bool> HasEntryForLineAsync(Guid sourceLineId, CancellationToken cancellationToken = default);

    /// <summary>Everything a document moved, so voiding it can record the opposite.</summary>
    Task<IReadOnlyList<StockLedgerEntry>> GetEntriesForDocumentAsync(
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task AddEntryAsync(StockLedgerEntry entry, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
