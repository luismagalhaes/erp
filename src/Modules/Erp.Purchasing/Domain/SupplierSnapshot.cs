namespace Erp.Purchasing.Domain;

/// <summary>
/// The supplier as it stood when the document was written. Copied rather than referenced, for the
/// same reason the sales documents copy the customer: editing the master file must never change
/// what a document already says. It matters more here — the tax id on a recorded supplier invoice
/// is the one that was on the paper, not the one on the file today.
/// </summary>
public sealed record SupplierSnapshot(
    string Code,
    string Name,
    string TaxId,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    string Country = "PT");
