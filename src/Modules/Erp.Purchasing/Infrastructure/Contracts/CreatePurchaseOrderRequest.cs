namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="Supplier">
/// The supplier's details, copied onto the order. Supplied by the caller rather than read here:
/// the supplier file belongs to Core, and this module does not depend on it — the same way Sales
/// receives the customer instead of fetching it.
/// </param>
/// <param name="Place">
/// True to send it to the supplier straight away instead of leaving it a draft.
/// </param>
public sealed record CreatePurchaseOrderRequest(
    Guid CompanyId,
    Guid SupplierId,
    PurchaseOrderSupplierDto Supplier,
    DateOnly OrderDate,
    Guid WarehouseId,
    IReadOnlyList<PurchaseOrderLineRequest> Lines,
    DateOnly? ExpectedDate = null,
    string? Notes = null,
    bool Place = false);
