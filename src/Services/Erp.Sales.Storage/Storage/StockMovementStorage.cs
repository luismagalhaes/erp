using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Storage;

public sealed class StockMovementStorage(SalesDbContext dbContext) : IStockMovementStorage
{
    public async Task<IReadOnlyList<StockMovement>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.StatusChanges)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.MovementDate)
            .ThenByDescending(x => x.SystemEntryDateUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.StockMovements
            .Include(x => x.Lines)
            .Include(x => x.StatusChanges)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Signature of the last movement of the series. Safe to read without extra locking because
    /// the caller already holds the update lock on the series row.
    /// </summary>
    public async Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default)
    {
        var hash = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.SeriesId == seriesId)
            .OrderByDescending(x => x.SequenceNumber)
            .Select(x => x.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return hash ?? string.Empty;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await dbContext.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task AddStatusChangeAsync(MovementStatusChange statusChange, CancellationToken cancellationToken = default)
    {
        await dbContext.MovementStatusChanges.AddAsync(statusChange, cancellationToken);
    }
}
