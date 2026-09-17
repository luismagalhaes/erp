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

/// <param name="Justificacao">Optional notes about why the series is being finalized (AT's "justificação").</param>
public sealed record FinalizeSeriesRequest(string? Justificacao = null);

/// <param name="ValidationCode">Code obtained outside the webservice, e.g. from the Portal das Finanças directly.</param>
public sealed record CommunicateSeriesManuallyRequest(string ValidationCode);

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

/// <summary>
/// The listing shape of a series. Written with init members rather than as a positional record so
/// it can be produced by an EF projection, which is what the OData listing runs.
/// </summary>
public sealed record SeriesListItemDto
{
    public Guid Id { get; init; }

    public Guid CompanyId { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string SeriesCode { get; init; } = string.Empty;

    public int CurrentSequence { get; init; }

    public string? ValidationCode { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool CanIssue { get; init; }

    /// <summary>None, In or Out.</summary>
    public string StockEffect { get; init; } = string.Empty;

    public bool SelfBilling { get; init; }

    public DateTime? CommunicatedAtUtc { get; init; }

    public DateTime? FinalizedAtUtc { get; init; }

    public DateTime? CancelledAtUtc { get; init; }
}
