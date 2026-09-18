using System.Net.Http.Json;
using Erp.Common.Statements;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

/// <summary>Purchase orders. Nothing here is a fiscal document, so orders can be rewritten.</summary>
public sealed class PurchasingApiClient(HttpClient http) : ApiClientBase(http)
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PurchaseOrderListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side order listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<PurchaseOrderListItem> Items, int Count, string? Error)> QueryOrdersAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<PurchaseOrderListItem>($"api/purchase-orders/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<PurchaseOrder?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/purchase-orders/{id}", cancellationToken);
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PendingOrderLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(PurchaseOrder? Order, string? Error)> CreateOrderAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/purchase-orders", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken), null);
    }

    public async Task<(PurchaseOrder? Order, string? Error)> UpdateOrderAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/purchase-orders/{id}", request, cancellationToken);

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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<GoodsReceiptListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side receipt listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<GoodsReceiptListItem> Items, int Count, string? Error)> QueryReceiptsAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<GoodsReceiptListItem>($"api/goods-receipts/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<GoodsReceipt?> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/goods-receipts/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<GoodsReceipt>(cancellationToken)
            : null;
    }

    public async Task<(GoodsReceipt? Receipt, string? Error)> CreateReceiptAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/goods-receipts", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<GoodsReceipt>(cancellationToken), null);
    }

    public async Task<(GoodsReceipt? Receipt, string? Error)> VoidReceiptAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PurchaseInvoiceListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side supplier invoice listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<PurchaseInvoiceListItem> Items, int Count, string? Error)> QueryInvoicesAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<PurchaseInvoiceListItem>($"api/purchase-invoices/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<PurchaseInvoice?> GetInvoiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/purchase-invoices/{id}", cancellationToken);
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<UninvoicedReceiptLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(PurchaseInvoice? Invoice, string? Error)> RecordInvoiceAsync(
        RecordPurchaseInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/purchase-invoices", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseInvoice>(cancellationToken), null);
    }

    public async Task<(PurchaseInvoice? Invoice, string? Error)> VoidInvoiceAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<SupplierReturnListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side supplier return listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<SupplierReturnListItem> Items, int Count, string? Error)> QueryReturnsAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<SupplierReturnListItem>($"api/supplier-returns/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<SupplierReturn?> GetReturnAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/supplier-returns/{id}", cancellationToken);
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

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<ReturnableReceiptLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(SupplierReturn? Return, string? Error)> CreateReturnAsync(
        CreateSupplierReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/supplier-returns", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierReturn>(cancellationToken), null);
    }

    public async Task<(SupplierReturn? Return, string? Error)> VoidReturnAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/supplier-returns/{id}/void", new VoidGoodsReceiptRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierReturn>(cancellationToken), null);
    }

    // --- Self-billing ---

    public async Task<(IReadOnlyList<SelfBilledInvoiceListItem> Items, string? Error)> GetSelfBilledInvoicesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/self-billed-invoices?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<SelfBilledInvoiceListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side self-billed invoice listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<SelfBilledInvoiceListItem> Items, int Count, string? Error)> QuerySelfBilledInvoicesAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<SelfBilledInvoiceListItem>($"api/self-billed-invoices/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<SelfBilledInvoice?> GetSelfBilledInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/self-billed-invoices/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SelfBilledInvoice>(cancellationToken)
            : null;
    }

    /// <summary>What has been received but not yet self-billed.</summary>
    public async Task<(IReadOnlyList<UnbilledReceiptLine> Items, string? Error)> GetUnbilledReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/self-billed-invoices/unbilled-receipts?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<UnbilledReceiptLine>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(SelfBilledInvoice? Invoice, string? Error)> IssueSelfBilledInvoiceAsync(
        IssueSelfBilledInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/self-billed-invoices", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SelfBilledInvoice>(cancellationToken), null);
    }

    public async Task<(SelfBilledInvoice? Invoice, string? Error)> AcceptSelfBilledInvoiceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/self-billed-invoices/{id}/accept", null, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SelfBilledInvoice>(cancellationToken), null);
    }

    public async Task<(SelfBilledInvoice? Invoice, string? Error)> VoidSelfBilledInvoiceAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/self-billed-invoices/{id}/void", new VoidSelfBilledInvoiceRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SelfBilledInvoice>(cancellationToken), null);
    }

    // --- Supplier statements ---

    /// <summary>A supplier's current account over a period; no dates means everything.</summary>
    public Task<(AccountStatement? Statement, string? Error)> GetSupplierStatementAsync(
        Guid companyId,
        Guid supplierId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default) =>
        GetSingleOrErrorAsync<AccountStatement>(
            $"api/supplier-statements?companyId={companyId}&supplierId={supplierId}{PeriodQuery(startDate, endDate)}",
            cancellationToken);

    // --- Supplier payments ---

    /// <summary>Server side supplier payment listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<SupplierPaymentListItem> Items, int Count, string? Error)> QuerySupplierPaymentsAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<SupplierPaymentListItem>($"api/supplier-payments/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<SupplierPayment?> GetSupplierPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/supplier-payments/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SupplierPayment>(cancellationToken)
            : null;
    }

    /// <summary>Supplier documents that still owe money, which are what a payment can be built from.</summary>
    public async Task<(IReadOnlyList<PayableDocument> Items, string? Error)> GetPayableDocumentsAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/supplier-payments/payable-documents?companyId={companyId}";

        if (supplierId is { } supplier)
            url += $"&supplierId={supplier}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PayableDocument>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(SupplierPayment? Payment, string? Error)> RecordSupplierPaymentAsync(
        CreateSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/supplier-payments", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierPayment>(cancellationToken), null);
    }

    public async Task<(SupplierPayment? Payment, string? Error)> VoidSupplierPaymentAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/supplier-payments/{id}/void", new VoidSupplierPaymentRequest(reason), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SupplierPayment>(cancellationToken), null);
    }

    private async Task<(PurchaseOrder? Order, string? Error)> PostAsync(
        string url,
        ClosePurchaseOrderRequest? content,
        CancellationToken cancellationToken)
    {
        var response = content is null
            ? await Http.PostAsync(url, content: null, cancellationToken)
            : await Http.PostAsJsonAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<PurchaseOrder>(cancellationToken), null);
    }
}
