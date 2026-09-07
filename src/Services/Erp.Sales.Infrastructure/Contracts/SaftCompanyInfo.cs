namespace Erp.Sales.Infrastructure.Contracts;

/// <summary>
/// Identification of the taxable entity for the SAF-T header. The Sales module does not own the
/// company file, so the caller supplies it.
/// </summary>
public sealed record SaftCompanyInfo(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? Email = null,
    string? Phone = null);
