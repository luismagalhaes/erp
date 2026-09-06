namespace Erp.Sales.Infrastructure.Contracts;

public sealed record CreateSeriesRequest(
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int InitialSequence = 1,
    string? EstablishmentCode = null);

/// <param name="ValidationCode">Code returned by the tax authority when the series is registered.</param>
public sealed record CommunicateSeriesRequest(string ValidationCode);

public sealed record SeriesListItemDto(
    Guid Id,
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int CurrentSequence,
    string? ValidationCode,
    string Status,
    bool CanIssue);
