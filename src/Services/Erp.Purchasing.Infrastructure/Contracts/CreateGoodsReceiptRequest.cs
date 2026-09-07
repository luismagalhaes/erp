namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="Supplier">
/// The supplier's details, copied onto the receipt. Supplied by the caller, like on the order: the
/// supplier file belongs to Core and this module does not depend on it.
/// </param>
/// <param name="SupplierDocumentNumber">
/// The number on the supplier's delivery note. Theirs, kept as a reference — we never adopt it as
/// ours, and the same number from two suppliers is not a conflict.
/// </param>
public sealed record CreateGoodsReceiptRequest(
    Guid CompanyId,
    Guid SupplierId,
    PurchaseOrderSupplierDto Supplier,
    DateOnly ReceiptDate,
    Guid WarehouseId,
    IReadOnlyList<GoodsReceiptLineRequest> Lines,
    string? SupplierDocumentNumber = null,
    DateOnly? SupplierDocumentDate = null,
    string? Notes = null);
