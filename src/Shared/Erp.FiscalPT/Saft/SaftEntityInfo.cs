namespace Erp.FiscalPT.Saft;

/// <summary>
/// The taxable entity the file is about — whose tax id goes in the header. On a billing file that
/// is the company; on a self-billing file it is the supplier, because the invoices are their sales.
/// </summary>
public sealed record SaftEntityInfo(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? Email = null,
    string? Phone = null);
