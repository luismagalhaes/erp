using Erp.Core.Domain;

namespace Erp.Core.Infrastructure.Storage;

public interface IWarehouseStorage
{
    Task<IReadOnlyList<Warehouse>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<Warehouse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(Guid companyId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>The company's default warehouse, or null while none is marked.</summary>
    Task<Warehouse?> GetDefaultAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
