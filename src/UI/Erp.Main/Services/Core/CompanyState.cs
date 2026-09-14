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

    public UserCompany? Selected =>
        Companies.FirstOrDefault(company => company.CompanyId == SelectedCompanyId);

    public bool HasCompany => SelectedCompanyId != Guid.Empty;

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
                Companies = await coreApi.GetMyCompaniesAsync(cancellationToken);
                SelectedCompanyId = Companies.FirstOrDefault()?.CompanyId ?? Guid.Empty;
                LoadError = Companies.Count == 0 ? (string?)localizer["CompanyState_NoCompanyAssociated"] : null;
            }
            catch (Exception ex)
            {
                // This runs from the layout, on every page. Letting anything escape here would
                // tear down the circuit and freeze the whole UI instead of showing the problem.
                Companies = [];
                SelectedCompanyId = Guid.Empty;
                LoadError = localizer["CompanyState_LoadError", ex.Message];
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
