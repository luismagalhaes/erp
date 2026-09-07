using Erp.Sales.Domain;

namespace Erp.Sales.Infrastructure.Storage;

public interface ISalesDocumentStorage
{
    Task<IReadOnlyList<SalesDocument>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<SalesDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documents of a period, fully loaded, in issuing order. This is what the SAF-T export reads:
    /// voided documents are included, because the file has to account for every number issued.
    /// </summary>
    Task<IReadOnlyList<SalesDocument>> GetForPeriodAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>Signature of the last document issued in the series, or empty for the first one.</summary>
    Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default);

    Task AddAsync(SalesDocument document, CancellationToken cancellationToken = default);

    Task AddStatusChangeAsync(DocumentStatusChange statusChange, CancellationToken cancellationToken = default);
}
