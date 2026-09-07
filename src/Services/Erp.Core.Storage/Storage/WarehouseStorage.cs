using Erp.Core.Domain;
using Erp.Core.Infrastructure.Storage;
using Erp.Core.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Storage;

public sealed class WarehouseStorage(CoreDbContext dbContext) : IWarehouseStorage
{
    public async Task<IReadOnlyList<Warehouse>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Warehouses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid companyId,
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        dbContext.Warehouses.AnyAsync(
            x => x.CompanyId == companyId && x.Code == code && (excludeId == null || x.Id != excludeId),
            cancellationToken);

    /// <summary>Tracked, because marking another warehouse as default has to step this one down.</summary>
    public Task<Warehouse?> GetDefaultAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        dbContext.Warehouses.FirstOrDefaultAsync(x => x.CompanyId == companyId && x.IsDefault, cancellationToken);

    public async Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default) =>
        await dbContext.Warehouses.AddAsync(warehouse, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
