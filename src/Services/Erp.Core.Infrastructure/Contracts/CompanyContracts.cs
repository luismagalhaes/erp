namespace Erp.Core.Infrastructure.Contracts;

public sealed record CompanyListItemDto(Guid Id, string Name, string? LegalName, string TaxId, bool IsActive);

public sealed record CompanyDetailDto(
    Guid Id,
    string Name,
    string? LegalName,
    string TaxId,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Email = null,
    string? Phone = null);

public sealed record UpdateCompanyRequest(
    string Name,
    string TaxId,
    string? LegalName,
    string? Email,
    string? Phone,
    bool IsActive);
