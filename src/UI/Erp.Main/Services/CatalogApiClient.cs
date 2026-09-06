using System.Net.Http.Json;
using Erp.Main.Models;

namespace Erp.Main.Services;

/// <summary>
/// Master data served by the Core module: products with their classification, customers and
/// suppliers. Every list is scoped to a company.
/// </summary>
public sealed class CatalogApiClient(HttpClient http)
{
    public HttpClient Http { get; } = http;

    // --- Brands ---

    public Task<(IReadOnlyList<Brand> Items, string? Error)> GetBrandsAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Brand>($"api/brands?companyId={companyId}", ct);

    public Task<Brand?> GetBrandAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Brand>($"api/brands/{id}", ct);

    public Task<string?> CreateBrandAsync(CreateBrandRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/brands", request, ct), ct);

    public Task<string?> UpdateBrandAsync(Guid id, UpdateBrandRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/brands/{id}", request, ct), ct);

    // --- Families ---

    public Task<(IReadOnlyList<ProductFamily> Items, string? Error)> GetFamiliesAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<ProductFamily>($"api/product-families?companyId={companyId}", ct);

    public Task<ProductFamily?> GetFamilyAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<ProductFamily>($"api/product-families/{id}", ct);

    public Task<string?> CreateFamilyAsync(CreateProductFamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/product-families", request, ct), ct);

    public Task<string?> UpdateFamilyAsync(Guid id, UpdateProductFamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/product-families/{id}", request, ct), ct);

    // --- Subfamilies ---

    public Task<(IReadOnlyList<ProductSubfamily> Items, string? Error)> GetSubfamiliesAsync(
        Guid companyId,
        Guid? familyId = null,
        CancellationToken ct = default)
    {
        var route = familyId is null
            ? $"api/product-subfamilies?companyId={companyId}"
            : $"api/product-subfamilies?companyId={companyId}&familyId={familyId}";

        return GetListAsync<ProductSubfamily>(route, ct);
    }

    public Task<ProductSubfamily?> GetSubfamilyAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<ProductSubfamily>($"api/product-subfamilies/{id}", ct);

    public Task<string?> CreateSubfamilyAsync(CreateProductSubfamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/product-subfamilies", request, ct), ct);

    public Task<string?> UpdateSubfamilyAsync(Guid id, UpdateProductSubfamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/product-subfamilies/{id}", request, ct), ct);

    // --- Products ---

    public Task<(IReadOnlyList<ProductListItem> Items, string? Error)> GetProductsAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<ProductListItem>($"api/products?companyId={companyId}", ct);

    public Task<ProductListItem?> GetProductAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<ProductListItem>($"api/products/{id}", ct);

    public Task<string?> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/products", request, ct), ct);

    public Task<string?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/products/{id}", request, ct), ct);

    // --- Customers and suppliers ---

    public Task<(IReadOnlyList<Partner> Items, string? Error)> GetCustomersAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Partner>($"api/customers?companyId={companyId}", ct);

    public Task<Partner?> GetCustomerAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Partner>($"api/customers/{id}", ct);

    public Task<string?> CreateCustomerAsync(CreatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/customers", request, ct), ct);

    public Task<string?> UpdateCustomerAsync(Guid id, UpdatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/customers/{id}", request, ct), ct);

    public Task<(IReadOnlyList<Partner> Items, string? Error)> GetSuppliersAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Partner>($"api/suppliers?companyId={companyId}", ct);

    public Task<Partner?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Partner>($"api/suppliers/{id}", ct);

    public Task<string?> CreateSupplierAsync(CreatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/suppliers", request, ct), ct);

    public Task<string?> UpdateSupplierAsync(Guid id, UpdatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/suppliers/{id}", request, ct), ct);

    // --- Plumbing ---

    private async Task<(IReadOnlyList<T> Items, string? Error)> GetListAsync<T>(string route, CancellationToken ct)
    {
        var response = await Http.GetAsync(route, ct);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, ct));

        var items = await response.Content.ReadFromJsonAsync<List<T>>(ct);
        return (items ?? [], null);
    }

    private async Task<T?> GetSingleAsync<T>(string route, CancellationToken ct)
    {
        var response = await Http.GetAsync(route, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>(ct) : default;
    }

    /// <summary>Returns null when it worked, or the message the API gave for refusing.</summary>
    private static async Task<string?> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        var response = await send();
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, ct);
    }
}
