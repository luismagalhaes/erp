using Erp.SeriesRegistry.Domain;
namespace Erp.SeriesRegistry.Infrastructure.Contracts;

/// <param name="StockEffect">
/// What documents of this series do to stock: None, In or Out. Null takes the default for the
/// document type. A string rather than the enum, so the wire format does not depend on how the
/// host happens to serialize enums.
/// </param>
/// <param name="SelfBilling">
/// True for a series that numbers invoices issued on behalf of a supplier. Only invoicing document
/// types can be self-billing, and such a series never numbers our own sales.
/// </param>
public sealed record CreateSeriesRequest(
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int InitialSequence = 1,
    string? EstablishmentCode = null,
    string? StockEffect = null,
    bool SelfBilling = false);

/// <param name="ValidationCode">Code returned by the tax authority when the series is registered.</param>
public sealed record CommunicateSeriesRequest(string ValidationCode);

/// <param name="StockEffect">None, In or Out.</param>
public sealed record SeriesListItemDto(
    Guid Id,
    Guid CompanyId,
    string DocumentType,
    string SeriesCode,
    int CurrentSequence,
    string? ValidationCode,
    string Status,
    bool CanIssue,
    string StockEffect,
    bool SelfBilling = false);
