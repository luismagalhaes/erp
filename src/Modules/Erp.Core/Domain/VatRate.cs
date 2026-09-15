namespace Erp.Core.Domain;

/// <summary>
/// A VAT rate for one fiscal region. Not scoped to a company: Portuguese VAT rates are set by law,
/// the same for every business in the same region — mainland, Açores or Madeira each have their
/// own percentage for Normal/Intermediate/Reduced/Isento, but no company sets its own.
/// </summary>
public sealed class VatRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>PT (mainland), PT-AC (Açores) or PT-MA (Madeira).</summary>
    public string FiscalRegion { get; set; } = string.Empty;

    /// <summary>NOR, INT, RED or ISE — the same rate tiers used across sales and purchasing documents.</summary>
    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public decimal Percentage { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? UpdatedAtUtc { get; set; }
}
