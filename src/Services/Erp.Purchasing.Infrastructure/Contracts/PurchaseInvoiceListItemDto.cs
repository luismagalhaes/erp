namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record PurchaseInvoiceListItemDto(
    Guid Id,
    string DocumentType,
    string SupplierDocumentNumber,
    DateOnly SupplierDocumentDate,
    DateOnly ReceivedDate,
    DateOnly? DueDate,
    string SupplierName,
    string SupplierTaxId,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    bool ReverseCharge,
    string Status,
    bool IsVoided);
