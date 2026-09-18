using Erp.Storage;

namespace Erp.Api.Security;

/// <summary>
/// The real <see cref="ICurrentUserContext"/>, populated once per request by
/// <see cref="CurrentUserContextMiddleware"/> before any controller action — and so before any
/// query <c>AppDbContext</c>'s tenant filter needs to evaluate — runs. Left at its default
/// (unrestricted) state outside a request, the same as <c>NullCurrentUserContext</c>, so nothing
/// that resolves this scope without going through the middleware (there is none today, but nothing
/// stops one being added later) is silently locked out either.
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    public bool IsHttpRequest { get; private set; }
    public bool IsSuperAdmin { get; private set; }
    public IReadOnlyCollection<Guid> AllowedCompanyIds { get; private set; } = [];

    internal void Load(bool isSuperAdmin, IReadOnlyCollection<Guid> allowedCompanyIds)
    {
        IsHttpRequest = true;
        IsSuperAdmin = isSuperAdmin;
        AllowedCompanyIds = allowedCompanyIds;
    }
}
