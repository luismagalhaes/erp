using System.Net.Http.Json;
using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>Shared helpers for reading API failures without leaking HTTP details into the UI.</summary>
public static class ApiResponse
{
    public static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return "Sem permissões para esta operação. Se as suas permissões mudaram há pouco, "
                 + "termine a sessão e volte a entrar: o token em cache ainda é o anterior.";
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            if (!string.IsNullOrWhiteSpace(problem?.Error))
                return problem.Error;
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            // Falls through to the status code message below.
        }

        return $"O pedido falhou com o estado {(int)response.StatusCode}.";
    }

    private sealed record ApiError(string? Error);
}

public class CoreApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    /// <summary>Companies the signed in user has access to.</summary>
    public async Task<IReadOnlyList<UserCompany>> GetMyCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await Http.GetFromJsonAsync<List<UserCompany>>("api/access/me/companies", cancellationToken);
        return companies ?? [];
    }

    public async Task<(IReadOnlyList<CompanyListItem> Companies, string? Error)> GetCompaniesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/companies", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var companies = await response.Content.ReadFromJsonAsync<List<CompanyListItem>>(cancellationToken);
        return (companies ?? [], null);
    }

    public async Task<CompanyDetail?> GetCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/companies/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CompanyDetail>(cancellationToken)
            : null;
    }

    public async Task<(CompanyDetail? Company, string? Error)> CreateCompanyAsync(
        CreateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/companies", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<CompanyDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<(CompanyDetail? Company, string? Error)> UpdateCompanyAsync(
        Guid id,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/companies/{id}", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<CompanyDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    /// <param name="companyId">Limits the result to one company; null returns every membership.</param>
    public async Task<(IReadOnlyList<UserCompanyAdmin> Items, string? Error)> GetUserCompaniesAsync(
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var route = companyId is null ? "api/user-companies" : $"api/user-companies?companyId={companyId}";
        var response = await Http.GetAsync(route, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var items = await response.Content.ReadFromJsonAsync<List<UserCompanyAdmin>>(cancellationToken);
        return (items ?? [], null);
    }

    public async Task<UserCompanyAdmin?> GetUserCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/user-companies/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<UserCompanyAdmin>(cancellationToken)
            : null;
    }

    public async Task<string?> CreateUserCompanyAsync(
        CreateUserCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/user-companies", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> UpdateUserCompanyAsync(
        Guid id,
        UpdateUserCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/user-companies/{id}", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> DeleteUserCompanyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.DeleteAsync($"api/user-companies/{id}", cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}

public class SalesApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<InvoiceListItem>> GetInvoicesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var invoices = await Http.GetFromJsonAsync<List<InvoiceListItem>>(
            $"api/invoices?companyId={companyId}", cancellationToken);

        return invoices ?? [];
    }

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

    public async Task<string?> CommunicateStockMovementAsync(Guid id, string atDocCodeId, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/stock-movements/{id}/communicate", new CommunicateStockMovementRequest(atDocCodeId), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    public async Task<string?> VoidStockMovementAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/stock-movements/{id}/void", new VoidStockMovementRequest(reason), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- SAF-T (PT) ---

    public async Task<(SaftPeriodSummary? Summary, string? Error)> GetSaftSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft/summary?companyId={companyId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SaftPeriodSummary>(cancellationToken), null);
    }

    /// <summary>
    /// Fetches the generated file. The name comes from the API, because it is the tax authority's
    /// naming convention and belongs with the code that builds the file.
    /// </summary>
    public async Task<(SaftFile? File, string? Error)> DownloadSaftAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft?companyId={companyId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"SAFT_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xml";

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var validationErrors = response.Headers.TryGetValues("X-Saft-Validation-Errors", out var values)
                               && int.TryParse(values.FirstOrDefault(), out var count)
            ? count
            : 0;

        return (new SaftFile(fileName, content, validationErrors), null);
    }

    /// <summary>
    /// The self-billing file, of type "S". It comes out one per supplier, with the supplier's tax id
    /// in the header, because the invoices in it are their sales and not ours.
    /// </summary>
    public async Task<(SaftFile? File, string? Error)> DownloadSelfBillingSaftAsync(
        Guid companyId,
        Guid supplierId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft/self-billing?companyId={companyId}&supplierId={supplierId}" +
            $"&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"SAFT_S_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xml";

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        var validationErrors = response.Headers.TryGetValues("X-Saft-Validation-Errors", out var values)
                               && int.TryParse(values.FirstOrDefault(), out var count)
            ? count
            : 0;

        return (new SaftFile(fileName, content, validationErrors), null);
    }

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

    public async Task<IReadOnlyList<SalesSeries>> GetSeriesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var series = await Http.GetFromJsonAsync<List<SalesSeries>>(
            $"api/series?companyId={companyId}", cancellationToken);

        return series ?? [];
    }

    public async Task<(SalesSeries? Series, string? Error)> CreateSeriesAsync(
        CreateSeriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/series", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SalesSeries>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<string?> CommunicateSeriesAsync(Guid id, string validationCode, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync(
            $"api/series/{id}/communicate", new CommunicateSeriesRequest(validationCode), cancellationToken);

        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

}

public class IdentityApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    /// <summary>Users from the Identity store, so they can be assigned to companies.</summary>
    public async Task<(IReadOnlyList<IdentityUser> Users, string? Error)> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync("api/users", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var users = await response.Content.ReadFromJsonAsync<List<IdentityUser>>(cancellationToken);
        return (users ?? [], null);
    }
}

public class NotificationApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<NotificationListItem>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await Http.GetFromJsonAsync<List<NotificationListItem>>("api/notifications", cancellationToken);
        return notifications ?? [];
    }

    public async Task<NotificationDetail?> GetNotificationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/notifications/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<NotificationDetail>(cancellationToken)
            : null;
    }

    /// <summary>Puts a failed email back in the queue. Returns the API message when it refuses.</summary>
    public async Task<string?> RequeueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/notifications/{id}/requeue", content: null, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }
}

