namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="Reason">
/// Why the goods are going back. Recorded because it is what the supplier's credit note will cite.
/// </param>
public sealed record CreateSupplierReturnRequest(
    Guid CompanyId,
    Guid SupplierId,
    PurchaseOrderSupplierDto Supplier,
    DateOnly ReturnDate,
    string Reason,
    IReadOnlyList<SupplierReturnLineRequest> Lines,
    string? Notes = null);
