namespace Erp.FiscalPT.Documents;

/// <summary>
/// Where a document series stands with the tax authority. A series is a fiscal register: it is
/// declared, gets a validation code back, is used, and is eventually closed.
/// </summary>
public enum SeriesStatus : byte
{
    /// <summary>Created locally, not yet communicated to the tax authority.</summary>
    Created = 0,

    /// <summary>Communicated, with a validation code returned by the tax authority.</summary>
    Communicated = 1,

    /// <summary>In use for issuing documents.</summary>
    Active = 2,

    /// <summary>Closed and communicated as finished; accepts no further documents.</summary>
    Finalized = 3,

    /// <summary>
    /// Communicated by mistake, then cancelled at the tax authority before any document used it.
    /// </summary>
    Cancelled = 4
}
