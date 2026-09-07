namespace Erp.Purchasing.Infrastructure.Storage;

public interface IPurchasingTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
