namespace Erp.Main.Models.Sales;

public sealed record InvoiceListItem(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    string Atcud,
    DateOnly DocumentDate,
    string CustomerName,
    string CustomerTaxId,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string Status,
    DateOnly? DueDate = null);
