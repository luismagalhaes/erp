using System.Net;
using System.Net.Http.Json;

namespace Erp.Dependencies.IdentityOnboarding;

public sealed class IdentityOnboardingClient(HttpClient httpClient) : IIdentityOnboardingClient
{
    private const string Endpoint = "api/onboarding-requests";

    public async Task<IdentityInviteResult> InviteAsync(IdentityInviteRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(Endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IdentityInviteResult>(cancellationToken)
            ?? throw new InvalidOperationException("The Identity host returned no answer to the invitation.");
    }

    public async Task<IReadOnlyList<IdentityOnboardingRequestDto>> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        await GetListAsync($"{Endpoint}?companyId={companyId}", cancellationToken);

    public async Task<IReadOnlyList<IdentityOnboardingRequestDto>> GetPendingForUserAsync(string userId, CancellationToken cancellationToken = default) =>
        await GetListAsync($"{Endpoint}/pending?userId={Uri.EscapeDataString(userId)}", cancellationToken);

    public async Task<bool> CompleteAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            $"{Endpoint}/{requestId}/complete", new { userId }, cancellationToken);

        return IsDone(response);
    }

    public async Task<bool> CancelAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsync($"{Endpoint}/{requestId}/cancel", null, cancellationToken);
        return IsDone(response);
    }

    private async Task<IReadOnlyList<IdentityOnboardingRequestDto>> GetListAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<IdentityOnboardingRequestDto>>(cancellationToken) ?? [];
    }

    /// <summary>A 404 means the request is gone or was already settled; anything else that fails is an error.</summary>
    private static bool IsDone(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }
}
