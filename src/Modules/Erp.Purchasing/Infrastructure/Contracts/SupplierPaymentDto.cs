namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record SupplierPaymentDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    string Number,
    DateOnly PaymentDate,
    string Status,
    bool IsVoided,
    PurchaseOrderSupplierDto Supplier,
    string? Description,
    decimal Total,
    DateTime CreatedAtUtc,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    IReadOnlyList<SupplierPaymentLineDto> Lines,
    IReadOnlyList<SupplierPaymentMethodDto> Methods);
