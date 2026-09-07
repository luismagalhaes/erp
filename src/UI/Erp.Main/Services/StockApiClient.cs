using System.Net.Http.Json;
using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>
/// Warehouses and stock. The warehouse file belongs to Core and the balances to Inventory, but
/// from the screen's side it is one subject, so one client serves both.
/// </summary>
public sealed class StockApiClient(HttpClient http)
{
    // --- Warehouses ---

    public async Task<(IReadOnlyList<Warehouse> Items, string? Error)> GetWarehousesAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/warehouses?companyId={companyId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<Warehouse>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<Warehouse?> GetWarehouseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/warehouses/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<Warehouse>(cancellationToken)
            : null;
    }

    public async Task<string?> CreateWarehouseAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/warehouses", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> UpdateWarehouseAsync(
        Guid id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/warehouses/{id}", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- Stock ---

    public async Task<(IReadOnlyList<StockBalance> Items, string? Error)> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/stock?companyId={companyId}";

        if (warehouseId is { } warehouse)
            url += $"&warehouseId={warehouse}";

        if (!string.IsNullOrWhiteSpace(productCode))
            url += $"&productCode={Uri.EscapeDataString(productCode)}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<StockBalance>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(IReadOnlyList<StockLedgerEntry> Items, string? Error)> GetLedgerAsync(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(
            $"api/stock/ledger?companyId={companyId}&warehouseId={warehouseId}&productCode={Uri.EscapeDataString(productCode)}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<StockLedgerEntry>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(StockCheckResult? Result, string? Error)> CheckAsync(
        Guid companyId,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/stock/check?companyId={companyId}";

        if (!string.IsNullOrWhiteSpace(productCode))
            url += $"&productCode={Uri.EscapeDataString(productCode)}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<StockCheckResult>(cancellationToken), null);
    }

    public async Task<string?> AdjustAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/stock/adjustments", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- Inventory counts ---

    public async Task<(IReadOnlyList<InventoryCount> Items, string? Error)> GetInventoryCountsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/inventory-counts?companyId={companyId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<InventoryCount>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<InventoryCount?> GetInventoryCountAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/inventory-counts/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<InventoryCount>(cancellationToken)
            : null;
    }

    public async Task<(InventoryCount? Count, string? Error)> OpenInventoryCountAsync(
        OpenInventoryCountRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/inventory-counts", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<InventoryCount>(cancellationToken), null);
    }

    /// <summary>Records what was found. Only the lines that changed need to travel.</summary>
    public async Task<(InventoryCount? Count, string? Error)> SetCountedAsync(
        Guid countId,
        IReadOnlyList<CountedLineRequest> lines,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync(
            $"api/inventory-counts/{countId}/lines", lines, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<InventoryCount>(cancellationToken), null);
    }

    public async Task<(InventoryCount? Count, string? Error)> CloseInventoryCountAsync(
        Guid countId,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsync(
            $"api/inventory-counts/{countId}/close", content: null, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<InventoryCount>(cancellationToken), null);
    }

    // --- Inventory communication file ---

    /// <param name="valued">
    /// True for the valued file (schema 2_01), required for periods from 2021; false for the older
    /// quantities-only one.
    /// </param>
    public async Task<(InventoryFileSummary? Summary, string? Error)> GetInventoryFileSummaryAsync(
        Guid companyId,
        int fiscalYear,
        DateOnly endDate,
        bool valued = true,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(
            $"api/inventory-file/summary?companyId={companyId}&fiscalYear={fiscalYear}&endDate={endDate:yyyy-MM-dd}&valued={valued}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<InventoryFileSummary>(cancellationToken), null);
    }

    public async Task<(InventoryFile? File, string? Error)> DownloadInventoryFileAsync(
        Guid companyId,
        int fiscalYear,
        DateOnly endDate,
        bool valued = true,
        CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(
            $"api/inventory-file?companyId={companyId}&fiscalYear={fiscalYear}&endDate={endDate:yyyy-MM-dd}&valued={valued}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"Inventario_{endDate:yyyyMMdd}.xml";

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return (new InventoryFile(
            fileName,
            content,
            ReadCount(response, "X-Inventory-Products-Without-Cost"),
            ReadCount(response, "X-Inventory-Validation-Errors")), null);
    }

    private static int ReadCount(HttpResponseMessage response, string header)
    {
        return response.Headers.TryGetValues(header, out var values)
               && int.TryParse(values.FirstOrDefault(), out var count)
            ? count
            : 0;
    }
}
