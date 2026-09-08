using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Inventory.Storage.Storage;

public sealed class InventoryCountStorage(AppDbContext dbContext) : IInventoryCountStorage
{
    public IQueryable<InventoryCountListItemDto> Query(Guid companyId) =>
        dbContext.Set<InventoryCount>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new InventoryCountListItemDto
            {
                Id = x.Id,
                WarehouseId = x.WarehouseId,
                Reference = x.Reference,
                Scope = x.Scope == InventoryCountScope.Total
                    ? "Total"
                    : x.Scope == InventoryCountScope.Warehouse ? "Warehouse" : "Products",
                Status = x.Status == InventoryCountStatus.Open ? "Open" : "Closed",
                CountDate = x.CountDate,
                CreatedAtUtc = x.CreatedAtUtc,
                ClosedAtUtc = x.ClosedAtUtc,
                LineCount = x.Lines.Count,
                LinesWithDifference = x.Lines.Count(line => line.CountedQuantity != line.SystemQuantity),
                IsOpen = x.Status == InventoryCountStatus.Open
            });

    public async Task<IReadOnlyList<InventoryCount>> GetAllAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<InventoryCount>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CountDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Tracked: the caller changes the counted quantities and closes it.</summary>
    public Task<InventoryCount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<InventoryCount>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> HasOpenCountAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Set<InventoryCount>().AnyAsync(
            x => x.CompanyId == companyId && x.Status == InventoryCountStatus.Open,
            cancellationToken);

    public async Task AddAsync(InventoryCount count, CancellationToken cancellationToken = default) =>
        await dbContext.Set<InventoryCount>().AddAsync(count, cancellationToken);

    public async Task AddLineAsync(InventoryCountLine line, CancellationToken cancellationToken = default) =>
        await dbContext.Set<InventoryCountLine>().AddAsync(line, cancellationToken);
}
