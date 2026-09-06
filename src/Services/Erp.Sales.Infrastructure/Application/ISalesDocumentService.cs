using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface ISalesDocumentService
{
    Task<IReadOnlyList<InvoiceListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<InvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a document: takes the next number in the series, signs it against the previous
    /// document hash and persists everything in a single transaction.
    /// </summary>
    Task<InvoiceDetailDto> IssueAsync(CreateInvoiceRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Voids a document without altering the original record.</summary>
    Task<InvoiceDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default);
}
