namespace Erp.Sales.Infrastructure.Storage;

public interface ISalesTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface ISalesUnitOfWork
{
    /// <summary>Starts the transaction that wraps numbering, signing and persistence.</summary>
    Task<ISalesTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
