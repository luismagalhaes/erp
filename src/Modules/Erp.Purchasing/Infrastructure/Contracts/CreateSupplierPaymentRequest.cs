namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A payment to record. The supplier comes already read from Core by the host, like on every other
/// purchasing document.
/// </summary>
public sealed record CreateSupplierPaymentRequest(
    Guid CompanyId,
    Guid SupplierId,
    PurchaseOrderSupplierDto Supplier,
    DateOnly PaymentDate,
    IReadOnlyList<SupplierPaymentLineRequest> Lines,
    IReadOnlyList<SupplierPaymentMethodRequest> Methods,
    string? Description = null);
