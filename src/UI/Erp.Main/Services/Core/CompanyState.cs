using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;
using Erp.Main.Resources;
using Microsoft.Extensions.Localization;

namespace Erp.Main.Services;

/// <summary>
/// Company the user is working on. Scoped to the circuit, loaded once and shared by every page,
/// so the selection in the header drives the whole application.
/// </summary>
public sealed class CompanyState(CoreApiClient coreApi, IStringLocalizer<MainResources> localizer)
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public IReadOnlyList<UserCompany> Companies { get; private set; } = [];

    public Guid SelectedCompanyId { get; private set; }

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    /// <summary>
    /// True when the list could not be read at all, as opposed to having been read and come back
    /// empty. The difference matters: an empty list means the user has to go through sign-up, while
    /// a failed load means the API is unreachable and sending them to sign-up would be a lie.
    /// </summary>
    public bool LoadFailed { get; private set; }

    public UserCompany? Selected =>
        Companies.FirstOrDefault(company => company.CompanyId == SelectedCompanyId);

    public bool HasCompany => SelectedCompanyId != Guid.Empty;

    /// <summary>The account exists but belongs to no company yet, so it still has to be signed up.</summary>
    public bool NeedsOnboarding => IsLoaded && !LoadFailed && Companies.Count == 0;

    /// <summary>Raised when the list is loaded or the user picks another company.</summary>
    public event Action? Changed;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
            return;

        await _loadGate.WaitAsync(cancellationToken);

        // Only a load that actually happened raises the event. Notifying on a no-op would
        // re-render the layout, which sets the parameters of the current page again, which
        // restarts its loading: the page would never settle.
        var loaded = false;

        try
        {
            if (IsLoaded)
                return;

            try
            {
                await ClaimInvitationsOnceAsync(cancellationToken);

                Companies = await coreApi.GetMyCompaniesAsync(cancellationToken);
                SelectedCompanyId = Companies.FirstOrDefault()?.CompanyId ?? Guid.Empty;
                LoadError = Companies.Count == 0 ? (string?)localizer["CompanyState_NoCompanyAssociated"] : null;
                LoadFailed = false;
            }
            catch (Exception ex)
            {
                // This runs from the layout, on every page. Letting anything escape here would
                // tear down the circuit and freeze the whole UI instead of showing the problem.
                Companies = [];
                SelectedCompanyId = Guid.Empty;
                LoadError = localizer["CompanyState_LoadError", ex.Message];
                LoadFailed = true;
            }

            IsLoaded = true;
            loaded = true;
        }
        finally
        {
            _loadGate.Release();

            if (loaded)
                Changed?.Invoke();
        }
    }

    /// <summary>
    /// True when this session just took up an invitation to a company. The roles in the current
    /// cookie predate it, so the layout signs the user in again to pick them up.
    /// </summary>
    public bool InvitationsClaimed { get; private set; }

    private bool _claimAttempted;

    /// <summary>
    /// A person who signed up from an invitation email has a company waiting for them: the ERP turns
    /// it into a membership the first time they open the application. Once per session, and never
    /// fatal: if it fails, the next session tries again.
    /// </summary>
    private async Task ClaimInvitationsOnceAsync(CancellationToken cancellationToken)
    {
        if (_claimAttempted)
            return;

        _claimAttempted = true;

        try
        {
            InvitationsClaimed = await coreApi.ClaimInvitationsAsync(cancellationToken) > 0;
        }
        catch (HttpRequestException)
        {
            InvitationsClaimed = false;
        }
    }

    public void Select(Guid companyId)
    {
        if (companyId == SelectedCompanyId)
            return;

        SelectedCompanyId = companyId;
        Changed?.Invoke();
    }

    /// <summary>Reloads the list after a company is created, renamed or assigned to someone.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsLoaded = false;
        var previous = SelectedCompanyId;

        await EnsureLoadedAsync(cancellationToken);

        if (!Companies.Any(company => company.CompanyId == previous))
            return;

        // Keep the user on the company they were working on; EnsureLoadedAsync already notified.
        if (SelectedCompanyId != previous)
        {
            SelectedCompanyId = previous;
            Changed?.Invoke();
        }
    }
}
