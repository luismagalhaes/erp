using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>
/// Company the user is working on. Scoped to the circuit, loaded once and shared by every page,
/// so the selection in the header drives the whole application.
/// </summary>
public sealed class CompanyState(CoreApiClient coreApi)
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

        try
        {
            if (IsLoaded)
                return;

            Companies = await coreApi.GetMyCompaniesAsync(cancellationToken);
            SelectedCompanyId = Companies.FirstOrDefault()?.CompanyId ?? Guid.Empty;
            LoadError = Companies.Count == 0 ? "A sua conta não tem nenhuma empresa associada." : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            LoadError = $"Não foi possível carregar as empresas: {ex.Message}";
        }
        finally
        {
            IsLoaded = true;
            _loadGate.Release();
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

    /// <summary>Reloads the list after a company is created or renamed.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsLoaded = false;
        var previous = SelectedCompanyId;

        await EnsureLoadedAsync(cancellationToken);

        if (Companies.Any(company => company.CompanyId == previous))
            SelectedCompanyId = previous;

        Changed?.Invoke();
    }
}
