namespace Erp.Core.Domain;

/// <summary>
/// A place where stock is held. Master data, like the product file: Inventory keeps balances per
/// warehouse, Sales ships from one and Purchasing receives into one. Modules refer to it by id
/// without a physical foreign key, the same way they refer to the company.
/// </summary>
public sealed class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    /// <summary>Short code, unique within the company.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "PT";

    /// <summary>The warehouse a document assumes when none is chosen. One per company.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
