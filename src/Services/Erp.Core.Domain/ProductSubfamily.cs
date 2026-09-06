namespace Erp.Core.Domain;

/// <summary>Second level of the product classification, always inside a family.</summary>
public sealed class ProductSubfamily
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid FamilyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ProductFamily Family { get; set; } = null!;

    public ICollection<Product> Products { get; set; } = [];
}
