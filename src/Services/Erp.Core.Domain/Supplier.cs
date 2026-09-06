namespace Erp.Core.Domain;

/// <summary>
/// Supplier master file, exported as the SAF-T Supplier table. Same shape as the customer,
/// kept apart because the two lists are managed by different people and rarely overlap.
/// </summary>
public sealed class Supplier
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    /// <summary>Code used inside the ERP; exported as SAF-T SupplierID.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string Country { get; set; } = "PT";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
