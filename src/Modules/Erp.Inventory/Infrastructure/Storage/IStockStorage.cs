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
    /// Every movement of the ledger, in the order it happened, reduced to what valuing it needs.
    /// </summary>
    /// <remarks>
    /// This replaced two SQL sums. A quantity can be added up in the database, but a weighted
    /// average cannot: it depends on the order the movements arrived in, so the rows have to be
    /// replayed. One reading serves both callers — the stock check, which compares the replay
    /// against the stored balances, and the inventory file, which needs the figures as they stood
    /// at the end of a period rather than today.
    /// </remarks>
    /// <param name="asAt">Null takes the whole ledger; a date stops at the end of that day.</param>
    Task<IReadOnlyList<LedgerMovement>> GetMovementsAsync(
        Guid companyId,
        DateOnly? asAt = null,
        string? productCode = null,
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
