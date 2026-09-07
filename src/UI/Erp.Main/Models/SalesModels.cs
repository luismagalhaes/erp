namespace Erp.Main.Models;

public sealed record UserCompany(Guid CompanyId, string CompanyName, string Role);

public sealed record CompanyListItem(Guid Id, string Name, string? LegalName, string TaxId, bool IsActive);

public sealed record CompanyDetail(
    Guid Id,
    string Name,
    string? LegalName,
    string TaxId,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT");

public sealed record UpdateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName,
    string? Email,
    string? Phone,
    bool IsActive,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT");

public sealed record SalesSeries(
    Guid Id,
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int CurrentSequence,
    string? ValidationCode,
    string Status,
    bool CanIssue);

public sealed record CreateSeriesRequest(
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int InitialSequence = 1,
    string? EstablishmentCode = null);

public sealed record CommunicateSeriesRequest(string ValidationCode);

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
    string? TaxExemptionReason = null);

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
    string? RectificationReason = null);

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

    public static bool IsRectifying(string code) =>
        Rectifying.Contains(code, StringComparer.Ordinal);

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
    string? TaxExemptionReason = null);

public sealed record CreateInvoiceRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly DocumentDate,
    CustomerRequest Customer,
    IReadOnlyList<CreateInvoiceLineRequest> Lines,
    Guid? RectifiedDocumentId = null,
    string? RectificationReason = null);

public sealed record VoidInvoiceRequest(string Reason);

public sealed record IdentityUser(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record UserCompanyAdmin(
    Guid Id,
    string UserId,
    Guid CompanyId,
    string CompanyName,
    string Role,
    bool IsActive);

public sealed record CreateUserCompanyRequest(string UserId, Guid CompanyId, string Role, bool IsActive = true);

public sealed record UpdateUserCompanyRequest(string Role, bool IsActive);

public sealed record NotificationListItem(
    Guid Id,
    string ToEmail,
    string Subject,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record NotificationDetail(
    Guid Id,
    string ToEmail,
    string Subject,
    string HtmlBody,
    string Status,
    int RetryCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
