namespace Erp.Core.Domain;

/// <summary>
/// Product master file, exported as the SAF-T Product table. It is shared master data: Sales
/// invoices it, Purchasing buys it and Inventory reports it. Documents keep their own copy of
/// these values, so editing a product never changes documents already issued.
/// </summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>SAF-T ProductType: P product, S service, O other, I taxes and fees.</summary>
    public string ProductType { get; set; } = "P";

    public string UnitOfMeasure { get; set; } = "UN";

    public decimal UnitPrice { get; set; }

    public string DefaultTaxCountryRegion { get; set; } = "PT";

    public string DefaultTaxCode { get; set; } = "NOR";

    public decimal DefaultTaxPercentage { get; set; } = 23m;

    /// <summary>EAN or other barcode, exported as SAF-T ProductNumberCode.</summary>
    public string? Barcode { get; set; }

    public Guid? FamilyId { get; set; }

    public Guid? SubfamilyId { get; set; }

    public Guid? BrandId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ProductFamily? Family { get; set; }

    public ProductSubfamily? Subfamily { get; set; }

    public Brand? Brand { get; set; }
}
