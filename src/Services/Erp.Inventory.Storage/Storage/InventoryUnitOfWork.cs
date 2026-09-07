using Erp.Common;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Inventory.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Inventory.Storage.Storage;

public sealed class InventoryUnitOfWork(InventoryDbContext dbContext, IAmbientDbTransaction ambient)
    : IInventoryUnitOfWork
{
    public async Task<IInventoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Another module may already have opened one for this request; joining it beats nesting.
        if (ambient.Current is { } existing)
        {
            if (dbContext.Database.CurrentTransaction is null)
                await dbContext.Database.UseTransactionAsync(existing, cancellationToken);

            return new JoinedTransaction();
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        ambient.Set(transaction.GetDbTransaction());

        return new InventoryTransaction(transaction, ambient);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class InventoryTransaction(IDbContextTransaction transaction, IAmbientDbTransaction ambient)
        : IInventoryTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            ambient.Set(null);
            await transaction.DisposeAsync();
        }
    }

    /// <summary>
    /// A transaction someone else owns. Committing and disposing are theirs to do, so this does
    /// nothing — otherwise the inner scope would end a transaction the outer one still needs.
    /// </summary>
    private sealed class JoinedTransaction : IInventoryTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
