using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

public interface IPurchaseInvoiceService
{
    Task<IReadOnlyList<PurchaseInvoiceListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<PurchaseInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What has been received but not yet invoiced. Recording an invoice starts here.</summary>
    Task<IReadOnlyList<UninvoicedReceiptLineDto>> GetUninvoicedReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a supplier's invoice into our books. Lines that come from a receipt move no stock —
    /// the receipt already brought the goods in. Lines that do not, and are for goods, bring them
    /// in themselves: the invoice that travelled with the lorry.
    /// </summary>
    Task<PurchaseInvoiceDto> RecordAsync(
        RecordPurchaseInvoiceRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Corrects a recorded invoice. Refused on one that brought goods into stock, which has to be
    /// voided instead.
    /// </summary>
    Task<PurchaseInvoiceDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseInvoiceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Strikes the record out, reversing any stock it brought in.</summary>
    Task<PurchaseInvoiceDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
