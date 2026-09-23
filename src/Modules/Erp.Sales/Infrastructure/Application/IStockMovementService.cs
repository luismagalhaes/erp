using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface IStockMovementService
{
    Task<IReadOnlyList<StockMovementListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing as a query, so filtering, sorting and paging can be applied by the database on
    /// behalf of the data grid.
    /// </summary>
    IQueryable<StockMovementListItemDto> Query(Guid companyId);

    Task<StockMovementDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a goods movement: takes the next number in the series, signs it against the
    /// previous document of that series and persists everything in a single transaction.
    /// </summary>
    Task<StockMovementDetailDto> IssueAsync(CreateStockMovementRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the document to the AT "Documentos de transporte" webservice and, once accepted,
    /// records the code it returned. Without it the goods cannot legally start moving.
    /// </summary>
    Task<StockMovementDetailDto?> CommunicateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers the ATDocCodeID by hand, for when the webservice could not be reached — missing
    /// credentials or an outage — and the code was obtained some other way (AT's own portal, for
    /// instance).
    /// </summary>
    Task<StockMovementDetailDto?> RegisterManualCodeAsync(Guid id, string atDocCodeId, CancellationToken cancellationToken = default);

    /// <summary>Voids a document without altering the original record.</summary>
    Task<StockMovementDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default);
}
