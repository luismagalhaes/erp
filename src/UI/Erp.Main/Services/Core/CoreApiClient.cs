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

    /// <summary>
    /// Seeds the company with auto-parts demo data and communicates its standard series with a
    /// fake validation code, so it looks and behaves like a working company for demonstrations.
    /// </summary>
    public async Task<(DemoDataResult? Result, string? Error)> ApplyDemoDataAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsync($"api/companies/{id}/apply-demo-data", null, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<DemoDataResult>(cancellationToken), null)
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

    // --- Subscriptions ---

    /// <param name="activeOnly">True for the sign-up page, which must not offer a retired package.</param>
    public async Task<(IReadOnlyList<SubscriptionPlan> Plans, string? Error)> GetSubscriptionPlansAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/subscription-plans?activeOnly={activeOnly}", cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ([], await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var plans = await response.Content.ReadFromJsonAsync<List<SubscriptionPlan>>(cancellationToken);
        return (plans ?? [], null);
    }

    public Task<(IReadOnlyList<SubscriptionPlan> Items, int Count, string? Error)> QuerySubscriptionPlansAsync(
        ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<SubscriptionPlan>($"api/subscription-plans/odata?{query.ToQueryString().TrimStart('&')}", cancellationToken);

    public async Task<SubscriptionPlan?> GetSubscriptionPlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/subscription-plans/{id}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SubscriptionPlan>(cancellationToken)
            : null;
    }

    public async Task<(SubscriptionPlan? Plan, string? Error)> CreateSubscriptionPlanAsync(
        CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/subscription-plans", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SubscriptionPlan>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public async Task<(SubscriptionPlan? Plan, string? Error)> UpdateSubscriptionPlanAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/subscription-plans/{id}", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<SubscriptionPlan>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }

    public Task<(IReadOnlyList<CompanySubscription> Items, int Count, string? Error)> QueryCompanySubscriptionsAsync(
        ODataQuery query, CancellationToken cancellationToken = default) =>
        GetODataAsync<CompanySubscription>($"api/company-subscriptions/odata?{query.ToQueryString().TrimStart('&')}", cancellationToken);

    /// <summary>Null when the company has never been put on a package.</summary>
    public async Task<CompanySubscription?> GetCompanySubscriptionAsync(
        Guid companyId, CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync($"api/company-subscriptions/{companyId}", cancellationToken);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<CompanySubscription>(cancellationToken)
            : null;
    }

    public async Task<string?> AssignCompanySubscriptionAsync(
        Guid companyId,
        AssignSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PutAsJsonAsync($"api/company-subscriptions/{companyId}", request, cancellationToken);
        return response.IsSuccessStatusCode ? null : await ApiResponse.ReadErrorAsync(response, cancellationToken);
    }

    // --- Sign-up ---

    /// <summary>Whether the signed-in user still has to create their company.</summary>
    public async Task<bool> HasCompanyAsync(CancellationToken cancellationToken = default)
    {
        var status = await Http.GetFromJsonAsync<OnboardingStatus>("api/companies/self-service/status", cancellationToken);
        return status?.HasCompany ?? false;
    }

    /// <summary>Creates the caller's own company and puts it on the package they picked.</summary>
    public async Task<(CompanyDetail? Company, string? Error)> CreateMyCompanyAsync(
        SelfServiceCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.PostAsJsonAsync("api/companies/self-service", request, cancellationToken);

        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<CompanyDetail>(cancellationToken), null)
            : (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));
    }
}

