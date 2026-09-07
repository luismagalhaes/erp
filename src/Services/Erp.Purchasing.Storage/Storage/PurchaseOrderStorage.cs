using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Storage;

public sealed class PurchaseOrderStorage(PurchasingDbContext dbContext) : IPurchaseOrderStorage
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
        return await dbContext.PurchaseOrders
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
        dbContext.PurchaseOrders
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
        _ = await dbContext.PurchaseOrders
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
        var orderId = await dbContext.PurchaseOrderLines
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
        return await dbContext.PurchaseOrders
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

    public Task<bool> NumberExistsAsync(Guid companyId, string number, CancellationToken cancellationToken = default) =>
        dbContext.PurchaseOrders.AnyAsync(x => x.CompanyId == companyId && x.Number == number, cancellationToken);

    public async Task<int> GetLastSequenceAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"ENC{year}/";

        var numbers = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Number.StartsWith(prefix))
            .Select(x => x.Number)
            .ToListAsync(cancellationToken);

        // Parsed in memory: the sequence is the tail of a string, and asking SQL Server to split it
        // would buy nothing on the handful of rows a company writes in a year.
        return numbers
            .Select(number => int.TryParse(number[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public async Task AddAsync(PurchaseOrder order, CancellationToken cancellationToken = default) =>
        await dbContext.PurchaseOrders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
