namespace Erp.Main.Models.Purchasing;

public sealed record CreateSupplierPaymentRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly PaymentDate,
    IReadOnlyList<SupplierPaymentLineRequest> Lines,
    IReadOnlyList<SupplierPaymentMethodRequest> Methods,
    string? Description = null);
