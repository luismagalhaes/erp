namespace Erp.Storage;

/// <summary>
/// The default <see cref="ICurrentUserContext"/> — always unrestricted, because <see cref="IsHttpRequest"/>
/// is false. Registered so <see cref="AppDbContext"/> can always be constructed, in every host that
/// calls <c>AddStorage</c>, whether or not that host ever overrides it with a real, HTTP aware one.
/// </summary>
public sealed class NullCurrentUserContext : ICurrentUserContext
{
    public bool IsHttpRequest => false;
    public bool IsSuperAdmin => false;
    public IReadOnlyCollection<Guid> AllowedCompanyIds => [];
}
