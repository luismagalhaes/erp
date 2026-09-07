using Erp.Common;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Inventory.Storage.Storage;

public sealed class StockStorage(InventoryDbContext dbContext, IAmbientDbTransaction ambient) : IStockStorage
{
    /// <summary>
    /// Joins the transaction the request is already in, when there is one. Without this the stock
    /// would be written in a transaction of its own and could commit while the document rolls back.
    /// </summary>
    private void EnsureEnlisted()
    {
        var current = ambient.Current;

        if (current is null || dbContext.Database.CurrentTransaction is not null)
            return;

        dbContext.Database.UseTransaction(current);
    }

    public async Task<IReadOnlyList<StockBalance>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.StockBalances
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
        EnsureEnlisted();

        // EF1002/EF1003: only the table name is interpolated, and it comes from the model. The
        // company, warehouse and product code are passed as parameters {0}, {1} and {2}.
#pragma warning disable EF1002, EF1003
        var locked = await dbContext.StockBalances
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<StockBalance>(dbContext)} WITH (UPDLOCK, ROWLOCK) " +
                "WHERE [CompanyId] = {0} AND [WarehouseId] = {1} AND [ProductCode] = {2}",
                companyId, warehouseId, productCode)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002, EF1003

        return locked;
    }

    public async Task AddBalanceAsync(StockBalance balance, CancellationToken cancellationToken = default) =>
        await dbContext.StockBalances.AddAsync(balance, cancellationToken);

    public async Task<IReadOnlyList<StockLedgerEntry>> GetEntriesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => warehouseId == null || x.WarehouseId == warehouseId)
            .Where(x => productCode == null || x.ProductCode == productCode)
            .OrderBy(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<(Guid WarehouseId, string ProductCode), (decimal Quantity, int EntryCount)>>
        SumLedgerAsync(Guid companyId, string? productCode = null, CancellationToken cancellationToken = default)
    {
        var sums = await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Where(x => productCode == null || x.ProductCode == productCode)
            .GroupBy(x => new { x.WarehouseId, x.ProductCode })
            .Select(group => new
            {
                group.Key.WarehouseId,
                group.Key.ProductCode,
                Quantity = group.Sum(x => x.Quantity),
                EntryCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        return sums.ToDictionary(
            x => (x.WarehouseId, x.ProductCode),
            x => (x.Quantity, x.EntryCount));
    }

    public async Task<IReadOnlyDictionary<string, (decimal Quantity, string Description)>> SumLedgerAsAtAsync(
        Guid companyId,
        DateOnly asAt,
        CancellationToken cancellationToken = default)
    {
        // Across every warehouse: the communication is per taxable entity, not per warehouse.
        var sums = await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.MovementDate <= asAt)
            .GroupBy(x => x.ProductCode)
            .Select(group => new
            {
                ProductCode = group.Key,
                Quantity = group.Sum(x => x.Quantity),
                Description = group.OrderByDescending(x => x.SystemEntryDateUtc)
                    .Select(x => x.ProductDescription)
                    .First()
            })
            .ToListAsync(cancellationToken);

        return sums.ToDictionary(x => x.ProductCode, x => (x.Quantity, x.Description));
    }

    public async Task<IReadOnlyList<StockLedgerEntry>> GetEntriesForDocumentAsync(
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        EnsureEnlisted();

        return await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(x => x.SourceDocumentId == sourceDocumentId)
            .OrderBy(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasEntryForLineAsync(Guid sourceLineId, CancellationToken cancellationToken = default) =>
        dbContext.StockLedgerEntries.AnyAsync(x => x.SourceLineId == sourceLineId, cancellationToken);

    public async Task AddEntryAsync(StockLedgerEntry entry, CancellationToken cancellationToken = default) =>
        await dbContext.StockLedgerEntries.AddAsync(entry, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnsureEnlisted();
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
