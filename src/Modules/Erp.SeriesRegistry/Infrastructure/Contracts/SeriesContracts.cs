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

/// <summary>
/// What a series still allows to be changed after it exists, which is very little.
/// </summary>
/// <remarks>
/// The document type, the code, the initial number and the self-billing flag are all communicated to
/// the tax authority and are woven into every number already issued — changing any of them would
/// leave the register disagreeing with the documents. What is left is the stock effect, which says
/// what <b>future</b> documents of the series do to the warehouse: a business decision, not a fiscal
/// one, and one that different businesses genuinely make differently for the same document type.
/// </remarks>
/// <param name="StockEffect">None, In or Out.</param>
public sealed record UpdateSeriesRequest(string StockEffect);

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
