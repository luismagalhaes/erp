using Erp.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Storage;

/// <summary>
/// The one unit of work, over the one context. A save writes everything the request accumulated,
/// whichever module put it there.
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Nesting is not allowed by EF and would be a bug here: whoever opened the outer one is
        // still counting on it, so an inner scope joins instead of starting its own.
        if (dbContext.Database.CurrentTransaction is not null)
            return new JoinedTransaction();

        return new OwnedTransaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task RunUnrestrictedAsync(Func<Task> action) => dbContext.RunUnrestricted(action);

    private sealed class OwnedTransaction(IDbContextTransaction transaction) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    /// <summary>
    /// A transaction someone else owns. Committing and disposing are theirs to do, so this does
    /// nothing — otherwise the inner scope would end a transaction the outer one still needs.
    /// </summary>
    private sealed class JoinedTransaction : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
