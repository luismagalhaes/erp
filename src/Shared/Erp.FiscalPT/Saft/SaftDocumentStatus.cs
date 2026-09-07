namespace Erp.FiscalPT.Saft;

/// <summary>
/// Current status of a document and when it got there. A voided document keeps its place in the
/// file with status "A" and the reason it was voided.
/// </summary>
public sealed class SaftDocumentStatus
{
    /// <summary>N normal, A voided, F billed, S self-billed, R summary.</summary>
    public string Status { get; init; } = "N";

    public DateTime StatusDate { get; init; }

    /// <summary>Required when the document is voided.</summary>
    public string? Reason { get; init; }

    /// <summary>User who put the document in this status.</summary>
    public string SourceId { get; init; } = SaftConstants.Unknown;

    /// <summary>P produced by the program, I integrated, M manually recorded.</summary>
    public string SourceBilling { get; init; } = "P";
}
