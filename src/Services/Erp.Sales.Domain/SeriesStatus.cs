namespace Erp.Sales.Domain;

public enum SeriesStatus
{
    /// <summary>Created locally, not yet communicated to the tax authority.</summary>
    Created = 0,

    /// <summary>Communicated, with a validation code returned by the tax authority.</summary>
    Communicated = 1,

    /// <summary>In use for issuing documents.</summary>
    Active = 2,

    /// <summary>Closed and communicated as finished; accepts no further documents.</summary>
    Finalized = 3
}
