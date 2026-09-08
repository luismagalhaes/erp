using System.Net.Http.Json;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

/// <summary>
/// Master data served by the Core module: products with their classification, customers and
/// suppliers. Every list is scoped to a company.
/// </summary>
public sealed class CatalogApiClient(HttpClient http) : ApiClientBase(http)
{
    // --- Brands ---

    public Task<(IReadOnlyList<Brand> Items, string? Error)> GetBrandsAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Brand>($"api/brands?companyId={companyId}", ct);

    /// <summary>
    /// Server side brand listing: filtering, sorting and paging are applied by the database through
    /// OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<Brand> Items, int Count, string? Error)> QueryBrandsAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<Brand>($"api/brands/odata?companyId={companyId}{query.ToQueryString()}", ct);

    public Task<Brand?> GetBrandAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Brand>($"api/brands/{id}", ct);

    public Task<string?> CreateBrandAsync(CreateBrandRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/brands", request, ct), ct);

    public Task<string?> UpdateBrandAsync(Guid id, UpdateBrandRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/brands/{id}", request, ct), ct);

    // --- Families ---

    public Task<(IReadOnlyList<ProductFamily> Items, string? Error)> GetFamiliesAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<ProductFamily>($"api/product-families?companyId={companyId}", ct);

    /// <summary>
    /// Server side family listing: filtering, sorting and paging are applied by the database through
    /// OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<ProductFamily> Items, int Count, string? Error)> QueryFamiliesAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<ProductFamily>($"api/product-families/odata?companyId={companyId}{query.ToQueryString()}", ct);

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

    /// <summary>
    /// Server side subfamily listing: filtering, sorting and paging are applied by the database
    /// through OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<ProductSubfamily> Items, int Count, string? Error)> QuerySubfamiliesAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<ProductSubfamily>($"api/product-subfamilies/odata?companyId={companyId}{query.ToQueryString()}", ct);

    public Task<ProductSubfamily?> GetSubfamilyAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<ProductSubfamily>($"api/product-subfamilies/{id}", ct);

    public Task<string?> CreateSubfamilyAsync(CreateProductSubfamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/product-subfamilies", request, ct), ct);

    public Task<string?> UpdateSubfamilyAsync(Guid id, UpdateProductSubfamilyRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/product-subfamilies/{id}", request, ct), ct);

    // --- Products ---

    public Task<(IReadOnlyList<ProductListItem> Items, string? Error)> GetProductsAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<ProductListItem>($"api/products?companyId={companyId}", ct);

    /// <summary>
    /// Server side product listing: filtering, sorting and paging are applied by the database
    /// through OData, so the grid never has to hold the whole product file.
    /// </summary>
    public Task<(IReadOnlyList<ProductListItem> Items, int Count, string? Error)> QueryProductsAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<ProductListItem>($"api/products/odata?companyId={companyId}{query.ToQueryString()}", ct);

    public Task<ProductListItem?> GetProductAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<ProductListItem>($"api/products/{id}", ct);

    public Task<string?> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/products", request, ct), ct);

    public Task<string?> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/products/{id}", request, ct), ct);

    // --- Customers and suppliers ---

    public Task<(IReadOnlyList<Partner> Items, string? Error)> GetCustomersAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Partner>($"api/customers?companyId={companyId}", ct);

    /// <summary>
    /// Server side customer listing: filtering, sorting and paging are applied by the database
    /// through OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<Partner> Items, int Count, string? Error)> QueryCustomersAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<Partner>($"api/customers/odata?companyId={companyId}{query.ToQueryString()}", ct);

    public Task<Partner?> GetCustomerAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Partner>($"api/customers/{id}", ct);

    public Task<string?> CreateCustomerAsync(CreatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/customers", request, ct), ct);

    public Task<string?> UpdateCustomerAsync(Guid id, UpdatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/customers/{id}", request, ct), ct);

    public Task<(IReadOnlyList<Partner> Items, string? Error)> GetSuppliersAsync(Guid companyId, CancellationToken ct = default) =>
        GetListAsync<Partner>($"api/suppliers?companyId={companyId}", ct);

    /// <summary>
    /// Server side supplier listing: filtering, sorting and paging are applied by the database
    /// through OData, so the grid only ever holds the window it is painting.
    /// </summary>
    public Task<(IReadOnlyList<Partner> Items, int Count, string? Error)> QuerySuppliersAsync(
        Guid companyId, ODataQuery query, CancellationToken ct = default) =>
        GetODataAsync<Partner>($"api/suppliers/odata?companyId={companyId}{query.ToQueryString()}", ct);

    public Task<Partner?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        GetSingleAsync<Partner>($"api/suppliers/{id}", ct);

    public Task<string?> CreateSupplierAsync(CreatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PostAsJsonAsync("api/suppliers", request, ct), ct);

    public Task<string?> UpdateSupplierAsync(Guid id, UpdatePartnerRequest request, CancellationToken ct = default) =>
        SendAsync(() => Http.PutAsJsonAsync($"api/suppliers/{id}", request, ct), ct);
}
