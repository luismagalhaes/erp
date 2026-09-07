namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// Corrects a recorded invoice. Nothing here was issued by us, so bookkeeping mistakes are fixed by
/// fixing them — except on a document that already brought goods into stock, which has to be voided.
/// </summary>
public sealed record UpdatePurchaseInvoiceRequest(
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    IReadOnlyList<PurchaseInvoiceLineRequest> Lines,
    Guid? WarehouseId = null,
    string? SupplierAtcud = null,
    DateOnly? DueDate = null,
    bool ReverseCharge = false,
    string? Notes = null);
