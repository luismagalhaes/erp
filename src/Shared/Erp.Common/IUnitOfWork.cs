namespace Erp.Common;

/// <summary>
/// Saving and transactions, shared by every module. One implementation over the one context.
/// </summary>
/// <remarks>
/// This used to be three interfaces and three implementations — one per module — each with the
/// dance of opening a transaction or joining somebody else's. With a single context there is
/// nothing to join: a save writes everything the request accumulated, whichever module put it there.
/// <para>
/// An explicit transaction is still needed wherever a row is locked. <c>WITH (UPDLOCK, ROWLOCK)</c>
/// only holds the row until the end of the transaction, and without one the lock is released as
/// soon as the read finishes — long before the write that depends on it.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
