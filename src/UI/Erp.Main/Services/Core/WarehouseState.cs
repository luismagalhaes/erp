using Erp.Main.Models.Core;

namespace Erp.Main.Services;

/// <summary>
/// Warehouse the user is working on. Scoped to the circuit, loaded once per selected company and
/// shared by every page, so the selection in the header drives every document that moves stock.
/// Stock is kept per warehouse, so there must always be one selected when the company has any.
/// </summary>
public sealed class WarehouseState : IDisposable
{
    private readonly StockApiClient _stockApi;
    private readonly CompanyState _companyState;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private Guid _loadedForCompanyId;

    public WarehouseState(StockApiClient stockApi, CompanyState companyState)
    {
        _stockApi = stockApi;
        _companyState = companyState;
        _companyState.Changed += OnCompanyChanged;
    }

    public IReadOnlyList<Warehouse> Warehouses { get; private set; } = [];

    public Guid SelectedWarehouseId { get; private set; }

    public bool IsLoaded { get; private set; }

    public string? LoadError { get; private set; }

    public Warehouse? Selected =>
        Warehouses.FirstOrDefault(warehouse => warehouse.Id == SelectedWarehouseId);

    public bool HasWarehouse => SelectedWarehouseId != Guid.Empty;

    /// <summary>Raised when the list is (re)loaded or the user picks another warehouse.</summary>
    public event Action? Changed;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoaded && _loadedForCompanyId == _companyState.SelectedCompanyId)
            return;

        await _loadGate.WaitAsync(cancellationToken);

        var loaded = false;

        try
        {
            if (IsLoaded && _loadedForCompanyId == _companyState.SelectedCompanyId)
                return;

            if (!_companyState.HasCompany)
            {
                Warehouses = [];
                SelectedWarehouseId = Guid.Empty;
                LoadError = null;
                IsLoaded = false;
                return;
            }

            try
            {
                var (warehouses, error) = await _stockApi.GetWarehousesAsync(
                    _companyState.SelectedCompanyId, cancellationToken);

                Warehouses = [.. warehouses.Where(warehouse => warehouse.IsActive)];
                SelectedWarehouseId = (Warehouses.FirstOrDefault(warehouse => warehouse.IsDefault)
                                       ?? Warehouses.FirstOrDefault())?.Id ?? Guid.Empty;
                LoadError = error;
            }
            catch (Exception ex)
            {
                // This runs from the layout, on every page. Letting anything escape here would
                // tear down the circuit and freeze the whole UI instead of showing the problem.
                Warehouses = [];
                SelectedWarehouseId = Guid.Empty;
                LoadError = ex.Message;
            }

            _loadedForCompanyId = _companyState.SelectedCompanyId;
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

    public void Select(Guid warehouseId)
    {
        if (warehouseId == SelectedWarehouseId)
            return;

        SelectedWarehouseId = warehouseId;
        Changed?.Invoke();
    }

    private async void OnCompanyChanged()
    {
        IsLoaded = false;
        await EnsureLoadedAsync();
    }

    public void Dispose() => _companyState.Changed -= OnCompanyChanged;
}
