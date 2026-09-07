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

    /// <summary>
    /// Takes an update lock on the document row for the rest of the transaction, and returns it
    /// fully loaded. This is what serializes crediting: two credit notes issued at the same time
    /// against the same document queue up here, so the second one sees the first one's credit.
    /// </summary>
    Task<SalesDocument?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of a document has already been credited by credit notes that are not voided.
    /// Debit notes are not counted: they add to what is owed instead of taking from it.
    /// </summary>
    Task<decimal> GetCreditedAmountAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each of those movement lines is already invoiced by documents that are not
    /// voided. Lines with nothing invoiced are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        IReadOnlyCollection<Guid> movementLineIds,
        CancellationToken cancellationToken = default);

    /// <summary>Signature of the last document issued in the series, or empty for the first one.</summary>
    Task<string> GetLastHashAsync(Guid seriesId, CancellationToken cancellationToken = default);

    Task AddAsync(SalesDocument document, CancellationToken cancellationToken = default);

    Task AddStatusChangeAsync(DocumentStatusChange statusChange, CancellationToken cancellationToken = default);
}
