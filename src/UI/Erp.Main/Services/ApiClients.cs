using System.Net.Http.Json;
using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>Shared helpers for reading API failures without leaking HTTP details into the UI.</summary>
public static class ApiResponse
{
    public static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
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

    public async Task<IReadOnlyList<CompanyListItem>> GetCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await Http.GetFromJsonAsync<List<CompanyListItem>>("api/companies", cancellationToken);
        return companies ?? [];
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

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var products = await Http.GetFromJsonAsync<List<ProductListItem>>(
            $"api/products?companyId={companyId}", cancellationToken);

        return products ?? [];
    }

    public async Task<ProductListItem?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/products/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ProductListItem>(cancellationToken)
            : null;
    }

    public async Task<(ProductListItem? Product, string? Error)> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/products", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<ProductListItem>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<(ProductListItem? Product, string? Error)> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/products/{id}", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<ProductListItem>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }
}

public class InventoryApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class PurchasingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class AccountingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}

public class ReportingApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;
}
