using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

/// <summary>
/// Invoices issued on a supplier's behalf, under article 36.º n.º 11 of the CIVA. The only
/// certified document this module produces.
/// </summary>
public interface ISelfBilledInvoiceService
{
    Task<IReadOnlyList<SelfBilledInvoiceListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<SelfBilledInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What has been received but not yet self-billed. Issuing normally starts here.</summary>
    Task<IReadOnlyList<UnbilledReceiptLineDto>> GetUnbilledReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the document: takes the next number from the series under a lock, chains the
    /// signature onto the previous one, and attaches the ATCUD and the QR code. Moves no stock —
    /// the goods came in on the receipt.
    /// </summary>
    Task<SelfBilledInvoiceDto> IssueAsync(
        IssueSelfBilledInvoiceRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the supplier accepted the document, which the regime requires of each one
    /// individually.
    /// </summary>
    Task<SelfBilledInvoiceDto?> AcceptAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids the document by appending a status change. The record itself is never touched, and the
    /// document stays in the SAF-T with status "A", as an issued document must.
    /// </summary>
    Task<SelfBilledInvoiceDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
