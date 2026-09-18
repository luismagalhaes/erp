namespace Erp.Storage;

/// <summary>
/// What <see cref="AppDbContext"/>'s per-company global query filter needs to know about the
/// caller, resolved once per request by the host — never here, since this project must never know
/// what a user or a company membership is (see the assembly's own doc comment on that).
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// False outside a real, authenticated web request — startup migrations, background workers and
    /// tests that build <see cref="AppDbContext"/> directly all leave tenant filtering switched off,
    /// since none of them are a caller whose access needs policing.
    /// </summary>
    bool IsHttpRequest { get; }

    /// <summary>A SuperAdmin manages every tenant, so nothing here is ever filtered for one.</summary>
    bool IsSuperAdmin { get; }

    /// <summary>
    /// The companies the caller belongs to. Only consulted when <see cref="IsHttpRequest"/> is true
    /// and <see cref="IsSuperAdmin"/> is false.
    /// </summary>
    IReadOnlyCollection<Guid> AllowedCompanyIds { get; }
}
