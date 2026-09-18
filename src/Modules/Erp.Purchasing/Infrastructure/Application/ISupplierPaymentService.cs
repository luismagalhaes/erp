using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

public interface ISupplierPaymentService
{
    Task<IReadOnlyList<SupplierPaymentListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>The listing shape, left open so the grid can filter, sort and page in the database.</summary>
    IQueryable<SupplierPaymentListItemDto> Query(Guid companyId);

    Task<SupplierPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Supplier invoices and self-billed invoices with money still owed, so a payment can be built
    /// from them. Voided documents and voided payments are left out of the calculation.
    /// </summary>
    Task<IReadOnlyList<PayableDocumentDto>> GetPayableDocumentsAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a payment: takes our next number and checks that every document still owes what is
    /// applied to it, all inside one transaction.
    /// </summary>
    Task<SupplierPaymentDto> RecordAsync(
        CreateSupplierPaymentRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Strikes the payment out; the documents it settled go back to being owed.</summary>
    Task<SupplierPaymentDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
