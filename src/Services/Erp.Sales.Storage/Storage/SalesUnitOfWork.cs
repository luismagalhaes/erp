using Erp.Sales.Infrastructure.Storage;
using Erp.Sales.Storage.Data;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Sales.Storage.Storage;

public sealed class SalesUnitOfWork(SalesDbContext dbContext) : ISalesUnitOfWork
{
    public async Task<ISalesTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new SalesTransaction(transaction);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class SalesTransaction(IDbContextTransaction transaction) : ISalesTransaction
    {
        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
        }
    }
}
