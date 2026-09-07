using System.Data.Common;

namespace Erp.Common;

/// <summary>
/// Holds the current transaction for the lifetime of one request. Registered scoped, so there is
/// one per request and no state leaks between them.
/// </summary>
public sealed class AmbientDbTransaction : IAmbientDbTransaction
{
    public DbTransaction? Current { get; private set; }

    public void Set(DbTransaction? transaction) => Current = transaction;
}
