using Erp.Common;

namespace Erp.Core.Domain;

/// <summary>Exempt from the tenant filter — see <see cref="SkipTenantFilterAttribute"/>.</summary>
[SkipTenantFilter]
public class UserCompany
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company Company { get; set; } = null!;
}
