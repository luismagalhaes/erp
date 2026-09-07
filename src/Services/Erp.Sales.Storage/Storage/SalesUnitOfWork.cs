using Erp.Common;
using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Sales.Storage.Storage;

public sealed class SalesUnitOfWork(SalesDbContext dbContext, IAmbientDbTransaction ambient) : ISalesUnitOfWork
{
    public async Task<ISalesTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Published so another module writing in the same request joins this transaction instead
        // of opening its own: the document and the stock it moves commit together or not at all.
        ambient.Set(transaction.GetDbTransaction());

        return new SalesTransaction(transaction, ambient);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class SalesTransaction(IDbContextTransaction transaction, IAmbientDbTransaction ambient)
        : ISalesTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            // Cleared before disposing, so nothing can join a transaction that is already over.
            ambient.Set(null);
            await transaction.DisposeAsync();
        }
    }
}
