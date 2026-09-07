using System.Data.Common;

namespace Erp.Common;

/// <summary>
/// The connection every module's <c>DbContext</c> uses within one request. The host builds it and
/// registers it scoped; the modules take it instead of a connection string.
/// </summary>
/// <remarks>
/// Two contexts can only share a transaction if they share the connection, and a document plus the
/// stock it moves have to be written together. The consequence to know about: a request must not
/// run queries on two contexts at the same time — one connection cannot serve two readers at once.
/// All our code awaits one call before starting the next, which is what makes this safe.
/// </remarks>
public sealed class SharedDbConnection(DbConnection connection) : IAsyncDisposable
{
    public DbConnection Connection { get; } = connection;

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
