using Erp.Sales.Domain;

namespace Erp.Sales.Infrastructure.Storage;

public interface IStockMovementStorage
{
    Task<IReadOnlyList<StockMovement>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<StockMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Movements with quantity still to invoice, fully loaded.</summary>
    Task<IReadOnlyList<StockMovement>> GetInvoiceableAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The movements that own those lines, each locked for the rest of the transaction. This is
    /// what stops two invoices from consuming the same delivery note at once, each blind to the
    /// other. The locks are taken in a fixed order, so concurrent issues cannot deadlock.
    /// </summary>
    Task<IReadOnlyList<StockMovement>> GetForUpdateByLinesAsync(
        IReadOnlyCollection<Guid> lineIds,
        CancellationToken cancellationToken = default);

    /// <summary>Movements of a period, fully loaded, in issuing order. Read by the SAF-T export.</summary>
    Task<IReadOnlyList<StockMovement>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>Signature of the last movement issued in the series, or empty for the first one.</summary>
    Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default);

    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);

    Task AddStatusChangeAsync(MovementStatusChange statusChange, CancellationToken cancellationToken = default);
}
