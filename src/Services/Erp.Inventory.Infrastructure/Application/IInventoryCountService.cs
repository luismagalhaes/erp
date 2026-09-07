using Erp.Inventory.Infrastructure.Contracts;

namespace Erp.Inventory.Infrastructure.Application;

public interface IInventoryCountService
{
    Task<IReadOnlyList<InventoryCountDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<InventoryCountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a count over the stock in scope, taking a picture of what the system holds. With
    /// <c>StartAtZero</c> every line opens at zero, which is how the stock is cleared before a
    /// fresh count.
    /// </summary>
    Task<InventoryCountDto> OpenAsync(
        OpenInventoryCountRequest request,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>Records what was found. Only while the count is open.</summary>
    Task<InventoryCountDto?> SetCountedAsync(
        Guid countId,
        IReadOnlyList<CountedLineRequest> lines,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the count and writes the differences to the ledger as adjustments, measured against
    /// the balances as they stand now — not against the picture taken when the count opened.
    /// </summary>
    Task<InventoryCountDto?> CloseAsync(
        Guid countId,
        string? userId,
        CancellationToken cancellationToken = default);
}
