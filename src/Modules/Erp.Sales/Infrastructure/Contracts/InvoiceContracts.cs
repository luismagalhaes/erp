namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="TaxId">Leave empty for an unidentified final consumer.</param>
public sealed record CustomerRequest(string? TaxId, string Name, string? Address, string Country = "PT");

/// <param name="OriginatingLineId">
/// The goods movement line this one invoices, when the invoice comes from a delivery note.
/// </param>
public sealed record CreateInvoiceLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxPercentage,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    Guid? OriginatingLineId = null);

/// <param name="RectifiedDocumentId">
/// The document being corrected. Required for a credit or debit note, refused on any other type.
/// </param>
/// <param name="RectificationReason">Why the document is being corrected.</param>
public sealed record CreateInvoiceRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly DocumentDate,
    CustomerRequest Customer,
    IReadOnlyList<CreateInvoiceLineRequest> Lines,
    Guid? RectifiedDocumentId = null,
    string? RectificationReason = null,
    Guid? WarehouseId = null);

public sealed record VoidInvoiceRequest(string Reason);

public sealed record InvoiceListItemDto(
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
    string Status);

/// <param name="TaxExemptionReason">
/// Required by law on the printed document whenever the line carries no tax.
/// </param>
public sealed record InvoiceLineDto(
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    string? OriginatingNumber = null,
    DateOnly? OriginatingDate = null);

public sealed record InvoiceTaxDto(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

/// <param name="DocumentType">FT, FS, FR, NC or ND — the printed document spells it out in full.</param>
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
    decimal CreditedAmount = 0m);
