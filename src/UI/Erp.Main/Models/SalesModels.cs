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
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Email = null,
    string? Phone = null);

public sealed record UpdateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName,
    string? Email,
    string? Phone,
    bool IsActive);

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

public sealed record ProductListItem(
    Guid Id,
    string ProductCode,
    string Description,
    string ProductType,
    string UnitOfMeasure,
    decimal UnitPrice,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    bool IsActive);

public sealed record CreateProductRequest(
    Guid CompanyId,
    string ProductCode,
    string Description,
    decimal UnitPrice,
    string ProductType = "P",
    string UnitOfMeasure = "UN",
    string DefaultTaxCountryRegion = "PT",
    string DefaultTaxCode = "NOR",
    decimal DefaultTaxPercentage = 23m);

public sealed record UpdateProductRequest(
    string Description,
    decimal UnitPrice,
    string ProductType,
    string UnitOfMeasure,
    string DefaultTaxCountryRegion,
    string DefaultTaxCode,
    decimal DefaultTaxPercentage,
    bool IsActive);

public sealed record InvoiceListItem(
    Guid Id,
    string DocumentNumber,
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
    decimal TaxAmount);

public sealed record InvoiceTax(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
    decimal TaxAmount);

public sealed record InvoiceDetail(
    Guid Id,
    string DocumentNumber,
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
    IReadOnlyList<InvoiceTax> Taxes);

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
    IReadOnlyList<CreateInvoiceLineRequest> Lines);

public sealed record VoidInvoiceRequest(string Reason);

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
