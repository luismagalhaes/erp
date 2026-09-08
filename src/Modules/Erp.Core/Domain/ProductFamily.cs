namespace Erp.Core.Domain;

/// <summary>Top level product classification.</summary>
public sealed class ProductFamily
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<ProductSubfamily> Subfamilies { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];
}
