using System.Data.Common;

namespace Erp.Common;

/// <summary>
/// Carries the transaction a request is running in, so a second module can join it without the two
/// modules knowing about each other.
/// </summary>
/// <remarks>
/// A document and the stock it moves have to be written together: one without the other is an
/// inconsistency nobody notices until the counts stop matching. The modules keep separate
/// <c>DbContext</c>s over the same database, so joining means sharing the connection and the
/// transaction — this is where the transaction is published and picked up.
/// </remarks>
public interface IAmbientDbTransaction
{
    /// <summary>The transaction in progress, or null when there is none.</summary>
    DbTransaction? Current { get; }

    /// <summary>Publishes the transaction. Called by whoever opens it; null when it ends.</summary>
    void Set(DbTransaction? transaction);
}
