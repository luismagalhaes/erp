using Erp.Sales.Infrastructure.Contracts;

namespace Erp.Sales.Infrastructure.Application;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default);

    Task<PaymentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invoices with money still owed, so a receipt can be built from them. Voided invoices and
    /// voided receipts are left out of the calculation.
    /// </summary>
    Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(
        Guid companyId,
        string? customerTaxId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a receipt: takes the next number in the series, signs it against the previous
    /// receipt of that series and persists everything in a single transaction.
    /// </summary>
    Task<PaymentDetailDto> IssueAsync(CreatePaymentRequest request, string? userId, CancellationToken cancellationToken = default);

    /// <summary>Voids a receipt without altering the original record; the invoices go back to owed.</summary>
    Task<PaymentDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default);
}
