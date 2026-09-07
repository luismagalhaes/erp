using Erp.Inventory.Domain;

namespace Erp.Inventory.Infrastructure.Storage;

public interface IInventoryCountStorage
{
    Task<IReadOnlyList<InventoryCount>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<InventoryCount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the company already has a count open. Two at once would fight each other.</summary>
    Task<bool> HasOpenCountAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task AddAsync(InventoryCount count, CancellationToken cancellationToken = default);
}
