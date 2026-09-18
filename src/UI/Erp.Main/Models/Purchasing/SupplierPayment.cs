namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPayment(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    DateOnly PaymentDate,
    string Status,
    bool IsVoided,
    PurchaseOrderSupplier Supplier,
    string? Description,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<SupplierPaymentLine> Lines,
    IReadOnlyList<SupplierPaymentMethod> Methods);
