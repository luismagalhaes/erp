using Erp.Main.Models.Core;

namespace Erp.Main.Services;

/// <summary>
/// The selected company's subscription, so the layout can warn once it has expired. Scoped to the
/// circuit and reloaded whenever the company changes, the same way <see cref="CompanyState"/> is.
/// </summary>
/// <remarks>
/// An expired subscription never blocks anything — it is a banner, not a gate. Nothing here refuses
/// a page, and a company that has no subscription row at all (every company created before
/// subscriptions existed) simply shows nothing.
/// </remarks>
public sealed class SubscriptionState(CoreApiClient coreApi)
{
    private Guid _loadedFor;

    public CompanySubscription? Current { get; private set; }

    public bool IsExpired => Current?.IsExpired ?? false;

    /// <summary>Raised once the subscription of a newly selected company has been read.</summary>
    public event Action? Changed;

    public async Task EnsureLoadedAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty || companyId == _loadedFor)
            return;

        _loadedFor = companyId;

        try
        {
            Current = await coreApi.GetCompanySubscriptionAsync(companyId, cancellationToken);
        }
        catch
        {
            // The banner is a courtesy: a company that cannot be told about its subscription is
            // better off working than staring at an error it can do nothing about.
            Current = null;
        }

        Changed?.Invoke();
    }
}
