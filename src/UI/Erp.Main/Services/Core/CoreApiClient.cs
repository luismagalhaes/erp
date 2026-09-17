using System.Net.Http.Json;
using Erp.Common;
using Erp.Main.Models.Core;
using Erp.Main.Models.Inventory;
using Erp.Main.Models.Notification;
using Erp.Main.Models.Purchasing;
using Erp.Main.Models.Sales;
using Erp.Main.Models.SeriesRegistry;

namespace Erp.Main.Services;

public class CoreApiClient(HttpClient http) : ApiClientBase(http)
{
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

    /// <summary>
    /// Server side company listing. Companies are global, so the companyId the grid passes is
    /// ignored here.
    /// </summary>
    public Task<(IReadOnlyList<CompanyListItem> Items, int Count, string? Error)> QueryCompaniesAsync(
        ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<CompanyListItem>($"api/companies/odata?{query.ToQueryString().TrimStart('&')}", cancellationToken);

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

    /// <summary>Whether the company has a WDT subutilizador registered for AT transport-document communication.</summary>
    public async Task<CompanyAtCredentialStatus?> GetCompanyAtCredentialStatusAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/companies/{id}/at-credentials", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CompanyAtCredentialStatus>(cancellationToken)
            : null;
    }

    /// <summary>Registers or replaces the company's WDT subutilizador. Write-only: the password is never read back.</summary>
    public async Task<string?> SetCompanyAtCredentialsAsync(
        Guid id, SetCompanyAtCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/companies/{id}/at-credentials", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
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

