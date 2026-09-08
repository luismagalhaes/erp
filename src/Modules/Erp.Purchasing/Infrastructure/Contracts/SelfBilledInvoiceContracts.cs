namespace Erp.Purchasing.Infrastructure.Contracts;

/// <param name="SeriesId">
/// A series marked for self-billing, and communicated to the tax authority. Nothing else may
/// number these documents.
/// </param>
/// <param name="SupplierAgreementReference">
/// The prior agreement with the supplier that article 36.º n.º 11 requires. Recorded on the
/// document so what authorises it is not merely folklore.
/// </param>
public sealed record IssueSelfBilledInvoiceRequest(
    Guid CompanyId,
    Guid SupplierId,
    Guid SeriesId,
    DateOnly IssueDate,
    PurchaseOrderSupplierDto Supplier,
    IReadOnlyList<SelfBilledInvoiceLineRequest> Lines,
    string? SupplierAgreementReference = null);

/// <param name="ReceiptLineId">
/// The goods receipt line being billed, when the document is raised from one. It moves no stock
/// either way — the goods came in on the receipt — but it is what stops the same delivery from
/// being billed twice.
/// </param>
public sealed record SelfBilledInvoiceLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxPercentage,
    string TaxCode = "NOR",
    string TaxCountryRegion = "PT",
    string UnitOfMeasure = "UN",
    Guid? ReceiptLineId = null,
    Guid? ReceiptId = null,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null);

/// <summary>
/// A row of the self-billed invoice list. Written with init members so it can be produced by an EF
/// projection, which is what the OData listing runs.
/// </summary>
public sealed record SelfBilledInvoiceListItemDto
{
    public Guid Id { get; init; }

    public string DocumentNumber { get; init; } = string.Empty;

    public string DocumentType { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public DateOnly IssueDate { get; init; }

    public string SupplierName { get; init; } = string.Empty;

    public string SupplierTaxId { get; init; } = string.Empty;

    public decimal NetTotal { get; init; }

    public decimal TaxPayable { get; init; }

    public decimal GrossTotal { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsAccepted { get; init; }
}

public sealed record SelfBilledInvoiceDto(
    Guid Id,
    Guid CompanyId,
    Guid SupplierId,
    Guid SeriesId,
    string DocumentType,
    string DocumentNumber,
    string Atcud,
    DateOnly IssueDate,
    DateTime SystemEntryDateUtc,
    string Status,
    DateTime? AcceptedBySupplierAtUtc,
    PurchaseOrderSupplierDto Supplier,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    // The 4 characters of the signature that go on the printed document, not the signature itself:
    // the full hash has no business leaving the server.
    string PrintableHash,
    string HashControl,
    string QrCodePayload,
    IReadOnlyList<SelfBilledInvoiceLineDto> Lines,
    IReadOnlyList<SelfBilledInvoiceTaxSummaryDto> TaxSummary);

public sealed record SelfBilledInvoiceLineDto(
    Guid Id,
    int LineNumber,
    Guid? ReceiptLineId,
    Guid? ReceiptId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount,
    string? TaxExemptionCode,
    string? TaxExemptionReason);

public sealed record SelfBilledInvoiceTaxSummaryDto(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record VoidSelfBilledInvoiceRequest(string Reason);

/// <summary>A goods receipt line that has not been self-billed yet.</summary>
public sealed record UnbilledReceiptLineDto(
    Guid ReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    Guid SupplierId,
    string SupplierName,
    Guid ReceiptLineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal ReceivedQuantity,
    decimal BilledQuantity,
    decimal PendingQuantity,
    decimal UnitCost);
