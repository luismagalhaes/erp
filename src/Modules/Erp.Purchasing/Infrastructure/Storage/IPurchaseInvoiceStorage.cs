using Erp.Purchasing.Domain;

namespace Erp.Purchasing.Infrastructure.Storage;

public interface IPurchaseInvoiceStorage
{
    Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this company already recorded this document from this supplier. The database also
    /// refuses it through a unique index — recording an invoice twice deducts the VAT twice, and
    /// that is not a mistake worth trusting a service check alone with.
    /// </summary>
    Task<bool> ExistsAsync(
        Guid companyId,
        string supplierTaxId,
        string supplierDocumentNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How much of each receipt line is already invoiced. Derived from the invoice lines rather
    /// than stored on the receipt, which is what keeps the receipt from ever needing to be
    /// rewritten. Voided invoices do not count — they invoice nothing.
    /// </summary>
    /// <param name="excludeInvoiceId">
    /// An invoice being rewritten, whose own lines must not count against its own ceiling.
    /// </param>
    Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        Guid? excludeInvoiceId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default);
}
