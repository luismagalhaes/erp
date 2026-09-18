namespace Erp.Main.Models.Purchasing;

public sealed record SupplierPaymentListItem(
    Guid Id,
    string Number,
    DateOnly PaymentDate,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    decimal Total,
    int SettledDocumentCount,
    string Status,
    bool IsVoided);
