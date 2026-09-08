using Erp.Common;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Erp.Storage;

namespace Erp.Inventory.Storage.Storage;

public sealed class StockStorage(AppDbContext dbContext) : IStockStorage
{
    // There used to be an EnsureEnlisted here, joining the transaction another module's context had
    // opened. With one context there is nothing to join: whoever opened the transaction opened it
    // on this very context, and these writes are already inside it.

    public async Task<IReadOnlyList<StockBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockBalance>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => warehouseId == null || x.WarehouseId == warehouseId)
            .Where(x => productCode == null || x.ProductCode == productCode)
            .OrderBy(x => x.ProductCode)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Locks the balance row, then loads it tracked. Two statements: the lock has to come from raw
    /// SQL, because EF has no way to express a hint.
    /// </summary>
    public async Task<StockBalance?> GetBalanceForUpdateAsync(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        CancellationToken cancellationToken = default)
    {

        // HOLDLOCK, and not only UPDLOCK, because this row very often does not exist yet: the first
        // movement of a product is what creates its balance. An update lock has nothing to hold on
        // to when nothing matches, so without HOLDLOCK several first movements all read null, all
        // create a balance, and all but one die on the unique index. HOLDLOCK takes a lock on the
        // key *range* instead, which is what makes "read, and insert if it was not there" safe.
        //
        // EF1002/EF1003: only the table name is interpolated, and it comes from the model. The
        // company, warehouse and product code are passed as parameters {0}, {1} and {2}.
#pragma warning disable EF1002, EF1003
        var locked = await dbContext.Set<StockBalance>()
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<StockBalance>(dbContext)} WITH (UPDLOCK, HOLDLOCK, ROWLOCK) " +
                "WHERE [CompanyId] = {0} AND [WarehouseId] = {1} AND [ProductCode] = {2}",
                companyId, warehouseId, productCode)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002, EF1003

        return locked;
    }

    public async Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken = default) =>
        await dbContext.Set<StockBalance>().AddAsync(balance, cancellationToken);

    public async Task<IReadOnlyList<StockLedgerEntry>> GetEntriesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => warehouseId == null || x.WarehouseId == warehouseId)
            .Where(x => productCode == null || x.ProductCode == productCode)
            .OrderBy(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LedgerMovement>> GetMovementsAsync(
        Guid companyId,
        DateOnly? asAt = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => asAt == null || x.MovementDate <= asAt)
            .Where(x => productCode == null || x.ProductCode == productCode)
            // The order the movements happened in, which is what the average depends on. The id
            // breaks ties, so two entries written in the same instant always replay the same way.
            .OrderBy(x => x.SystemEntryDateUtc)
            .ThenBy(x => x.Id)
            .Select(x => new LedgerMovement(
                x.WarehouseId,
                x.ProductCode,
                x.ProductDescription,
                x.Quantity,
                x.UnitCost))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockLedgerEntry>> GetEntriesForDocumentAsync(
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {

        return await dbContext.Set<StockLedgerEntry>()
            .AsNoTracking()
            .Where(x => x.SourceDocumentId == sourceDocumentId)
            .OrderBy(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasEntryForLineAsync(Guid sourceLineId, CancellationToken cancellationToken = default) =>
        dbContext.Set<StockLedgerEntry>().AnyAsync(x => x.SourceLineId == sourceLineId, cancellationToken);

    public async Task AddEntryAsync(StockLedgerEntry entry, CancellationToken cancellationToken = default) =>
        await dbContext.Set<StockLedgerEntry>().AddAsync(entry, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
