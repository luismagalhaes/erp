using System.Net.Http.Json;
using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>Purchase orders. Nothing here is a fiscal document, so orders can be rewritten.</summary>
public sealed class PurchasingApiClient(HttpClient http)
{
    public async Task<(IReadOnlyList<PurchaseOrderListItem> Items, string? Error)> GetOrdersAsync(
        Guid companyId,
        Guid? supplierId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/purchase-orders?companyId={companyId}&openOnly={openOnly}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PurchaseOrderListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/purchase-orders/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken)
            : null;
    }

    /// <summary>What suppliers still owe, line by line.</summary>
    public async Task<(IReadOnlyList<PendingOrderLine> Items, string? Error)> GetPendingLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/purchase-orders/pending?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PendingOrderLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(PurchaseOrder? Order, string? Error)> CreateOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/purchase-orders", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken), null);
    }

    public async Task<(PurchaseOrder? Order, string? Error)> UpdateOrderAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PutAsJsonAsync($"api/purchase-orders/{id}", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken), null);
    }

    public Task<(PurchaseOrder? Order, string? Error)> PlaceOrderAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/purchase-orders/{id}/place", content: null, cancellationToken);

    public Task<(PurchaseOrder? Order, string? Error)> CloseOrderAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/purchase-orders/{id}/close", new ClosePurchaseOrderRequest(reason), cancellationToken);

    public Task<(PurchaseOrder? Order, string? Error)> CancelOrderAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default) =>
        PostAsync($"api/purchase-orders/{id}/cancel", new ClosePurchaseOrderRequest(reason), cancellationToken);

    // --- Goods receipts ---

    public async Task<(IReadOnlyList<GoodsReceiptListItem> Items, string? Error)> GetReceiptsAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/goods-receipts?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<GoodsReceiptListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<GoodsReceipt?> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/goods-receipts/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<GoodsReceipt>(cancellationToken)
            : null;
    }

    public async Task<(GoodsReceipt? Receipt, string? Error)> CreateReceiptAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/goods-receipts", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<GoodsReceipt>(cancellationToken), null);
    }

    public async Task<(GoodsReceipt? Receipt, string? Error)> VoidReceiptAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/goods-receipts/{id}/void", new VoidGoodsReceiptRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<GoodsReceipt>(cancellationToken), null);
    }

    // --- Supplier invoices ---

    public async Task<(IReadOnlyList<PurchaseInvoiceListItem> Items, string? Error)> GetInvoicesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/purchase-invoices?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PurchaseInvoiceListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<PurchaseInvoice?> GetInvoiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/purchase-invoices/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PurchaseInvoice>(cancellationToken)
            : null;
    }

    /// <summary>What has been received but not yet invoiced.</summary>
    public async Task<(IReadOnlyList<UninvoicedReceiptLine> Items, string? Error)> GetUninvoicedReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/purchase-invoices/uninvoiced-receipts?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<UninvoicedReceiptLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(PurchaseInvoice? Invoice, string? Error)> RecordInvoiceAsync(
        RecordPurchaseInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/purchase-invoices", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseInvoice>(cancellationToken), null);
    }

    public async Task<(PurchaseInvoice? Invoice, string? Error)> VoidInvoiceAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/purchase-invoices/{id}/void", new VoidPurchaseInvoiceRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseInvoice>(cancellationToken), null);
    }

    // --- Returns to supplier ---

    public async Task<(IReadOnlyList<SupplierReturnListItem> Items, string? Error)> GetReturnsAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/supplier-returns?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<SupplierReturnListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<SupplierReturn?> GetReturnAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync($"api/supplier-returns/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SupplierReturn>(cancellationToken)
            : null;
    }

    /// <summary>What is still in hand from each receipt, and so could go back.</summary>
    public async Task<(IReadOnlyList<ReturnableReceiptLine> Items, string? Error)> GetReturnableLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/supplier-returns/returnable?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<ReturnableReceiptLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(SupplierReturn? Return, string? Error)> CreateReturnAsync(
        CreateSupplierReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync("api/supplier-returns", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierReturn>(cancellationToken), null);
    }

    public async Task<(SupplierReturn? Return, string? Error)> VoidReturnAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync(
            $"api/supplier-returns/{id}/void", new VoidGoodsReceiptRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierReturn>(cancellationToken), null);
    }

    private async Task<(PurchaseOrder? Order, string? Error)> PostAsync(
        string url,
        ClosePurchaseOrderRequest? content,
        CancellationToken cancellationToken)
    {
        var response = content is null
            ? await http.PostAsync(url, content: null, cancellationToken)
            : await http.PostAsJsonAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken), null);
    }
}
