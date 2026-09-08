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
    string Status);

public sealed record InvoiceLine(
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

/// <summary>A delivery note line with quantity still to invoice.</summary>
public sealed record PendingMovementLine(
    Guid MovementId,
    Guid LineId,
    string DocumentNumber,
    string MovementType,
    DateOnly MovementDate,
    string PartyName,
    string PartyTaxId,
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal MovedQuantity,
    decimal InvoicedQuantity,
    decimal PendingQuantity,
    decimal UnitPrice,
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    string? TaxExemptionCode,
    string? TaxExemptionReason);

public sealed record InvoiceTax(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

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
    decimal CreditedAmount = 0m);

/// <summary>
/// Invoicing document types with the designation the printed document has to spell out in full.
/// </summary>
public static class InvoiceTypes
{
    public static readonly (string Code, string Label)[] All =
    [
        ("FT", "Fatura"),
        ("FS", "Fatura simplificada"),
        ("FR", "Fatura-recibo"),
        ("NC", "Nota de crédito"),
        ("ND", "Nota de débito")
    ];

    /// <summary>
    /// Types that correct another document, and so must name it and say why — artigo 36.º n.º 5
    /// do CIVA. The API enforces the same rule.
    /// </summary>
    public static readonly string[] Rectifying = ["NC", "ND"];

    /// <summary>Types that create a sale, as opposed to correcting one.</summary>
    public static readonly string[] Sale = ["FT", "FS", "FR"];

    public static bool IsRectifying(string code) =>
        Rectifying.Contains(code, StringComparer.Ordinal);

    public static bool IsSale(string code) =>
        Sale.Contains(code, StringComparer.Ordinal);

    public static string Describe(string code) =>
        All.FirstOrDefault(type => type.Code == code).Label ?? code;
}

public sealed record CustomerRequest(string? TaxId, string Name, string? Address, string Country = "PT");

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

public sealed record CreateInvoiceRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly DocumentDate,
    CustomerRequest Customer,
    IReadOnlyList<CreateInvoiceLineRequest> Lines,
    Guid? RectifiedDocumentId = null,
    string? RectificationReason = null);

public sealed record VoidInvoiceRequest(string Reason);
