namespace Erp.Main.Models.Core;

public sealed record UserCompany(Guid CompanyId, string CompanyName, string Role);

public sealed record CompanyListItem(Guid Id, string Name, string? LegalName, string TaxId, bool IsActive);

public sealed record CompanyDetail(
    Guid Id,
    string Name,
    string? LegalName,
    string TaxId,
    string? Email,
    string? Phone,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT");

public sealed record UpdateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName,
    string? Email,
    string? Phone,
    bool IsActive,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT");

/// <summary>The registered WDT subutilizador and when it was set, without the password.</summary>
public sealed record CompanyAtCredentialStatus(string SubUserId, DateTime UpdatedAtUtc);

public sealed record SetCompanyAtCredentialsRequest(string SubUserId, string Password);

public sealed record IdentityUser(
    string Id,
    string Email,
    string FullName,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

public sealed record UserCompanyAdmin(
    Guid Id,
    string UserId,
    Guid CompanyId,
    string CompanyName,
    string Role,
    bool IsActive);

public sealed record CreateUserCompanyRequest(string UserId, Guid CompanyId, string Role, bool IsActive = true);

public sealed record UpdateUserCompanyRequest(string Role, bool IsActive);
