namespace Erp.Inventory.Infrastructure.Storage;

public interface IInventoryTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}

public interface IInventoryUnitOfWork
{
    /// <summary>
    /// Starts a transaction. Closing a count writes one adjustment per line plus the balances they
    /// move; half of that landing would leave the warehouse in a state nobody asked for.
    /// </summary>
    Task<IInventoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
