using System.Net.Http.Headers;
using Erp.Identity.Infrastructure.Application;

namespace Erp.Identity.Dependencies.Services;

/// <summary>Attaches the host service token to outgoing calls to other ERP services.</summary>
public sealed class ServiceTokenHandler(IServiceTokenProvider tokenProvider) : DelegatingHandler
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
