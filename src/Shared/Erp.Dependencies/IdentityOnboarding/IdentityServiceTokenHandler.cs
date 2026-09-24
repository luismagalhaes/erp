using System.Net.Http.Headers;

namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>Attaches the API's own service token to calls to the Identity host.</summary>
public sealed class IdentityServiceTokenHandler(IdentityServiceTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(request, cancellationToken);
    }
}
