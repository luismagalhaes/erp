namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="SupplierDocumentNumber">
/// The number on their document, as it came. Unique per supplier, never renumbered: recording the
/// same invoice twice would deduct the VAT twice.
/// </param>
/// <param name="WarehouseId">
/// Where goods this invoice brings in are stored. Only needed when the invoice carries goods that
/// no receipt already brought in — a services invoice needs none.
/// </param>
public sealed record RecordPurchaseInvoiceRequest(
    Guid CompanyId,
    Guid SupplierId,
    PurchaseOrderSupplierDto Supplier,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    IReadOnlyList<PurchaseInvoiceLineRequest> Lines,
    Guid? WarehouseId = null,
    string? SupplierAtcud = null,
    DateOnly? DueDate = null,
    bool ReverseCharge = false,
    string? Notes = null);
