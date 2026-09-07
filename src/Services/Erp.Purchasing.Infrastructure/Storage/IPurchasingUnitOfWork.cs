namespace Erp.Purchasing.Infrastructure.Storage;

public interface IPurchasingUnitOfWork
{
    /// <summary>
    /// Starts a transaction, or joins the one this request is already in. Receiving goods writes
    /// the receipt, moves the stock and credits the order: any of those landing alone would leave
    /// the warehouse and the order disagreeing about what arrived.
    /// </summary>
    Task<IPurchasingTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
