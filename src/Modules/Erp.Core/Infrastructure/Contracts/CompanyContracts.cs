namespace Erp.Core.Infrastructure.Contracts;

/// <summary>
/// The company as the backoffice listing sees it. Init properties instead of a positional record
/// because EF Core cannot bind members over a constructor projection, which breaks any $orderby or
/// $filter the OData grid applies on top of the query.
/// </summary>
public sealed record CompanyListItemDto
{
    public CompanyListItemDto()
    {
    }

    public CompanyListItemDto(Guid id, string name, string? legalName, string taxId, bool isActive)
    {
        Id = id;
        Name = name;
        LegalName = legalName;
        TaxId = taxId;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed record CompanyDetailDto(
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
