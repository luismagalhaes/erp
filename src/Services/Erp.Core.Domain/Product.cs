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

    /// <summary>Selling price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// What the item costs the company. Used to value stock in the inventory communication, which
    /// since Portaria 126/2019 carries the valuation and not only the quantities.
    /// </summary>
    /// <remarks>
    /// A standard cost held on the product file. Proper costing — weighted average or FIFO, moved
    /// by each purchase — is a separate piece of work; until it exists, this is what values stock.
    /// </remarks>
    public decimal UnitCost { get; set; }

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
