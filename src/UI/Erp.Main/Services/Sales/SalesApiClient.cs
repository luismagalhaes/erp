using System.Net.Http.Json;
using Erp.Common;
using Erp.Common.Statements;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

public class SalesApiClient(HttpClient http) : ApiClientBase(http)
{
    public async Task<IReadOnlyList<InvoiceListItem>> GetInvoicesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var invoices = await Http.GetFromJsonAsync<List<InvoiceListItem>>(
            $"api/invoices?companyId={companyId}", cancellationToken);

        return invoices ?? [];
    }

    /// <summary>
    /// Server side document listing: filtering, sorting and paging are applied by the database
    /// through OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<InvoiceListItem> Items, int Count, string? Error)> QueryInvoicesAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<InvoiceListItem>($"api/invoices/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<InvoiceDetail?> GetInvoiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/invoices/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<InvoiceDetail>(cancellationToken)
            : null;
    }

    /// <summary>Delivery note lines with quantity still to invoice.</summary>
    public async Task<(IReadOnlyList<PendingMovementLine> Items, string? Error)> GetPendingMovementLinesAsync(
        Guid companyId,
        string? partyTaxId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/invoices/pending-movements?companyId={companyId}";

        if (!string.IsNullOrWhiteSpace(partyTaxId))
            url += $"&partyTaxId={Uri.EscapeDataString(partyTaxId)}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PendingMovementLine>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Issues a document. Returns the error message from the API when it refuses.</summary>
    public async Task<(InvoiceDetail? Invoice, string? Error)> IssueInvoiceAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/invoices", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<InvoiceDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<string?> VoidInvoiceAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync($"api/invoices/{id}/void", new VoidInvoiceRequest(reason), cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- Goods movements ---

    public async Task<(IReadOnlyList<StockMovementListItem> Items, string? Error)> GetStockMovementsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/stock-movements?companyId={companyId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<StockMovementListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side movement listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<StockMovementListItem> Items, int Count, string? Error)> QueryStockMovementsAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<StockMovementListItem>($"api/stock-movements/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<StockMovementDetail?> GetStockMovementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/stock-movements/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<StockMovementDetail>(cancellationToken)
            : null;
    }

    public async Task<(StockMovementDetail? Movement, string? Error)> IssueStockMovementAsync(
        CreateStockMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/stock-movements", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<StockMovementDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<string?> CommunicateStockMovementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/stock-movements/{id}/communicate", null, cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> VoidStockMovementAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/stock-movements/{id}/void", new VoidStockMovementRequest(reason), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- Customer statements ---

    /// <summary>A customer's current account over a period; no dates means everything.</summary>
    public Task<(AccountStatement? Statement, string? Error)> GetCustomerStatementAsync(
        Guid companyId,
        Guid customerId,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken cancellationToken = default) =>
        GetSingleOrErrorAsync<AccountStatement>(
            $"api/customer-statements?companyId={companyId}&customerId={customerId}{PeriodQuery(startDate, endDate)}",
            cancellationToken);

    // --- Receipts ---

    public async Task<(IReadOnlyList<PaymentListItem> Items, string? Error)> GetPaymentsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/payments?companyId={companyId}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<PaymentListItem>>(cancellationToken);
        return (items ?? [], null);
    }

    /// <summary>Server side receipt listing, filtered, sorted and paged by the database.</summary>
    public Task<(IReadOnlyList<PaymentListItem> Items, int Count, string? Error)> QueryPaymentsAsync(
        Guid companyId, ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<PaymentListItem>($"api/payments/odata?companyId={companyId}{query.ToQueryString()}", cancellationToken);

    public async Task<PaymentDetail?> GetPaymentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/payments/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<PaymentDetail>(cancellationToken)
            : null;
    }

    /// <summary>Invoices that still owe money, which are what a receipt can be built from.</summary>
    public async Task<(IReadOnlyList<OutstandingInvoice> Items, string? Error)> GetOutstandingInvoicesAsync(
        Guid companyId,
        string? customerTaxId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/payments/outstanding-invoices?companyId={companyId}";

        if (!string.IsNullOrWhiteSpace(customerTaxId))
            url += $"&customerTaxId={Uri.EscapeDataString(customerTaxId)}";

        var response = await Http.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<OutstandingInvoice>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<(PaymentDetail? Payment, string? Error)> IssuePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/payments", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<PaymentDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<string?> VoidPaymentAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/payments/{id}/void", new VoidPaymentRequest(reason), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}
