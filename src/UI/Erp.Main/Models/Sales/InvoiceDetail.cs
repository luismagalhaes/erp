namespace Erp.Main.Models.Sales;

/// <param name="GrossLinesTotal">Total ilíquido: quantity times price, before discounts.</param>
/// <param name="NetTotal">Total líquido: the taxable total, after discounts.</param>
public sealed record InvoiceDetail(
    Guid Id,
    string DocumentNumber,
    string DocumentType,
    string Atcud,
    DateOnly DocumentDate,
    DateTime SystemEntryDateUtc,
    string Status,
    string CustomerName,
    string CustomerTaxId,
    string? CustomerAddress,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string PrintableHash,
    string QrCodePayload,
    IReadOnlyList<InvoiceLine> Lines,
    IReadOnlyList<InvoiceTax> Taxes,
    string? RectifiedDocumentNumber = null,
    string? RectificationReason = null,
    decimal CreditedAmount = 0m,
    DateOnly? DueDate = null,
    string? CustomerPostalCode = null,
    string? CustomerCity = null,
    decimal GrossLinesTotal = 0m,
    decimal DiscountTotal = 0m,
    IReadOnlyList<InvoicePayment>? Payments = null);
