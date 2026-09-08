namespace Erp.Core.Domain;

/// <summary>
/// Customer master file, exported as the SAF-T Customer table. Documents snapshot these values
/// at issuing time, so later edits never change what was invoiced.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    /// <summary>Code used inside the ERP; exported as SAF-T CustomerID.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>999999990 stands for an unidentified final consumer.</summary>
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
