using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface IStockMovementService
{
    Task<IReadOnlyList<StockMovementListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<StockMovementDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a goods movement: takes the next number in the series, signs it against the
    /// previous document of that series and persists everything in a single transaction.
    /// </summary>
    Task<StockMovementDetailDto> IssueAsync(CreateStockMovementRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Records the code returned by the tax authority for this document.</summary>
    Task<StockMovementDetailDto?> CommunicateAsync(Guid id, string atDocCodeId, CancellationToken cancellationToken = default);

    /// <summary>Voids a document without altering the original record.</summary>
    Task<StockMovementDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default);
}
