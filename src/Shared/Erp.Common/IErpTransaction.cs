namespace Erp.Common;

/// <summary>
/// A database transaction, seen from a service that has no business knowing about EF Core.
/// </summary>
public interface IErpTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
