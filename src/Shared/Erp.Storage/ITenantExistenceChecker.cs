namespace Erp.Storage;

/// <summary>
/// The one place allowed to look past <see cref="AppDbContext"/>'s tenant filter — and only to
/// answer a yes/no question, never to read or return a row from another company. Its only consumer
/// today is telling a genuinely missing row apart from one the caller simply cannot see, for a
/// <c>{id:guid}</c> action that carries no companyId of its own to check up front (see
/// <c>TenantAwareNotFoundFilter</c> in Erp.Api). Split out as its own interface, separate from
/// <see cref="AppDbContext"/> itself, purely so that filter can be unit tested against a
/// substitute instead of a real database.
/// </summary>
public interface ITenantExistenceChecker
{
    /// <summary>Whether a row with this id exists for any company, ignoring the tenant filter.</summary>
    Task<bool> ExistsForAnotherCompanyAsync(Type entityType, Guid id, CancellationToken cancellationToken);
}
