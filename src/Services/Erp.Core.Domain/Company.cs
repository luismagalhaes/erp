namespace Erp.Core.Domain;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string TaxId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<UserCompany> UserCompanies { get; set; } = new List<UserCompany>();
}
