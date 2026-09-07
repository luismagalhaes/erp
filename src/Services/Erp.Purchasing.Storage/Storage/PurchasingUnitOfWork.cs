using Erp.Common;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Purchasing.Storage.Storage;

public sealed class PurchasingUnitOfWork(PurchasingDbContext dbContext, IAmbientDbTransaction ambient)
    : IPurchasingUnitOfWork
{
    public async Task<IPurchasingTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Another module may already have opened one for this request; joining it beats nesting.
        if (ambient.Current is { } existing)
        {
            if (dbContext.Database.CurrentTransaction is null)
                await dbContext.Database.UseTransactionAsync(existing, cancellationToken);

            return new JoinedTransaction();
        }

        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Published so the Inventory module writes its ledger entries inside this same transaction.
        ambient.Set(transaction.GetDbTransaction());

        return new PurchasingTransaction(transaction, ambient);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    private sealed class PurchasingTransaction(IDbContextTransaction transaction, IAmbientDbTransaction ambient)
        : IPurchasingTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

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
    private sealed class JoinedTransaction : IPurchasingTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
