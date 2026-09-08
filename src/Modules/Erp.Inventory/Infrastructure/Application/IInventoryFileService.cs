using Erp.Inventory.Infrastructure.Contracts;

namespace Erp.Inventory.Infrastructure.Application;

public interface IInventoryFileService
{
    /// <summary>
    /// Builds the inventory communication file for a period, from the stock held on the last day
    /// of that period.
    /// </summary>
    Task<InventoryFileResultDto> BuildAsync(
        InventoryFileRequest request,
        CancellationToken cancellationToken = default);
}
