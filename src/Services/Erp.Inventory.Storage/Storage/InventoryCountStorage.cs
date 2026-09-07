using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Inventory.Storage.Storage;

public sealed class InventoryCountStorage(InventoryDbContext dbContext) : IInventoryCountStorage
{
    public async Task<IReadOnlyList<InventoryCount>> GetAllAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.InventoryCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CountDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Tracked: the caller changes the counted quantities and closes it.</summary>
    public Task<InventoryCount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.InventoryCounts
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasOpenCountAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.InventoryCounts.AnyAsync(
            x => x.CompanyId == companyId && x.Status == InventoryCountStatus.Open,
            cancellationToken);

    public async Task AddAsync(InventoryCount count, CancellationToken cancellationToken = default) =>
        await dbContext.InventoryCounts.AddAsync(count, cancellationToken);
}
