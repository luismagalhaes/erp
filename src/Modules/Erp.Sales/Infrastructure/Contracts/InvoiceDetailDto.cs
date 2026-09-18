namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="DocumentType">FT, FS, FR, NC or ND — the printed document spells it out in full.</param>
/// <param name="GrossLinesTotal">Quantity times price over all lines: the "total ilíquido".</param>
/// <param name="DiscountTotal">What the line discounts took off.</param>
/// <param name="NetTotal">The taxable total, after discounts: the "total líquido".</param>
public sealed record InvoiceDetailDto(
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
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<InvoiceTaxDto> Taxes,
    string? RectifiedDocumentNumber = null,
    string? RectificationReason = null,
    decimal CreditedAmount = 0m,
    DateOnly? DueDate = null,
    string? CustomerPostalCode = null,
    string? CustomerCity = null,
    decimal GrossLinesTotal = 0m,
    decimal DiscountTotal = 0m,
    IReadOnlyList<InvoicePaymentDto>? Payments = null);
