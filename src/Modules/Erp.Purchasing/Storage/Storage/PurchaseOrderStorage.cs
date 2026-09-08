using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Purchasing.Storage.Storage;

public sealed class PurchaseOrderStorage(ErpDbContext dbContext) : IPurchaseOrderStorage
{
    /// <summary>Statuses that still expect goods.</summary>
    private static readonly PurchaseOrderStatus[] OpenStatuses =
        [PurchaseOrderStatus.Placed, PurchaseOrderStatus.PartiallyReceived];

    public async Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .Where(x => !openOnly || OpenStatuses.Contains(x.Status))
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<PurchaseOrder>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <summary>
    /// Locks the order row, then loads it tracked with its lines. Two statements: the lock has to
    /// come from raw SQL, because EF has no way to express a hint.
    /// </summary>
    public async Task<PurchaseOrder?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // EF1002/EF1003: only the table name is interpolated, and it comes from the model. The id
        // is passed as parameter {0}.
#pragma warning disable EF1002, EF1003
        _ = await dbContext.Set<PurchaseOrder>()
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<PurchaseOrder>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                id)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002, EF1003

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PurchaseOrder?> GetForUpdateByLineAsync(
        Guid orderLineId,
        CancellationToken cancellationToken = default)
    {
        var orderId = await dbContext.Set<PurchaseOrderLine>()
            .AsNoTracking()
            .Where(x => x.Id == orderLineId)
            .Select(x => (Guid?)x.OrderId)
            .FirstOrDefaultAsync(cancellationToken);

        return orderId is null ? null : await GetForUpdateAsync(orderId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetPendingAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<PurchaseOrder>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .Where(x => OpenStatuses.Contains(x.Status))
            // Translated to SQL, so the pending quantity is spelled out rather than taken from the
            // computed property, which EF cannot see.
            .Where(x => x.Lines.Any(line => line.Quantity > line.ReceivedQuantity))
            .OrderBy(x => x.ExpectedDate ?? DateOnly.MaxValue)
            .ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) =>
        await dbContext.Set<PurchaseOrder>().AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
