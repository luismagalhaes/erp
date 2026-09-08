using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface ISalesDocumentService
{
    /// <summary>
    /// Goods movement lines with quantity still to invoice, so an invoice can be built from the
    /// delivery notes instead of being typed again.
    /// </summary>
    Task<IReadOnlyList<PendingMovementLineDto>> GetPendingMovementLinesAsync(
        Guid companyId,
        string? partyTaxId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing as a query, so filtering, sorting and paging can be applied by the database on
    /// behalf of the data grid.
    /// </summary>
    IQueryable<InvoiceListItemDto> Query(Guid companyId);

    Task<InvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a document: takes the next number in the series, signs it against the previous
    /// document hash and persists everything in a single transaction.
    /// </summary>
    Task<InvoiceDetailDto> IssueAsync(CreateInvoiceRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Voids a document without altering the original record.</summary>
    Task<InvoiceDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default);
}
