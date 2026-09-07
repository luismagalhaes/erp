namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="StartDate">First day covered, inclusive.</param>
/// <param name="EndDate">Last day covered, inclusive.</param>
public sealed record SaftExportRequest(
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    SaftCompanyInfo Company);
