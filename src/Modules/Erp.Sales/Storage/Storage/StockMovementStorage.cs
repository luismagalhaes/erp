using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Sales.Storage.Storage;

public sealed class StockMovementStorage(ErpDbContext dbContext) : IStockMovementStorage
{
    public async Task<IReadOnlyList<StockMovement>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.MovementDate)
            .ThenByDescending(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockMovement>()
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovement>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId && x.MovementDate >= startDate && x.MovementDate <= endDate)
            .OrderBy(x => x.SeriesId)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovement>> GetInvoiceableAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .Where(x => !x.PartyIsSupplier)
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StockMovement>> GetForUpdateByLinesAsync(
        IReadOnlyCollection<Guid> lineIds,
        CancellationToken cancellationToken = default)
    {
        if (lineIds.Count == 0)
            return [];

        // Ordered so that two transactions touching the same movements always take the locks in
        // the same sequence, which is what keeps them from deadlocking against each other.
        var movementIds = await dbContext.Set<StockMovementLine>()
            .AsNoTracking()
            .Where(line => lineIds.Contains(line.Id))
            .Select(line => line.MovementId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);

        var movements = new List<StockMovement>(movementIds.Count);

        foreach (var movementId in movementIds)
        {
            // EF1002: only the table name is interpolated, and it comes from the model.
#pragma warning disable EF1002
            var locked = await dbContext.Set<StockMovement>()
                .FromSqlRaw(
                    $"SELECT * FROM {QualifiedTableName.For<StockMovement>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                    movementId)
                .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002

            if (locked is null)
                continue;

            // Loaded separately, because the locking statement alone brings neither the lines nor
            // the status changes that say whether the movement still stands.
            var movement = await GetByIdAsync(movementId, cancellationToken);

            if (movement is not null)
                movements.Add(movement);
        }

        return movements;
    }

    /// <summary>
    /// Signature of the last movement of the series. Safe to read without extra locking because
    /// the caller already holds the update lock on the series row.
    /// </summary>
    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.Set<StockMovement>()
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<StockMovement>().AddAsync(movement, cancellationToken);
    }

    public async Task AddStatusChangeAsync(MovementStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<MovementStatusChange>().AddAsync(statusChange, cancellationToken);
    }
}
