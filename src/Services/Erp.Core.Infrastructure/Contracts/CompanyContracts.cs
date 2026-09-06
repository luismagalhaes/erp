namespace Erp.Core.Infrastructure.Contracts;

public sealed record CompanyListItemDto(Guid Id, string Name, string? LegalName, string TaxId, bool IsActive);
