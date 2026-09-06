using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Erp.Identity.Dependencies.Configuration;
using Erp.Identity.Infrastructure.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Identity.Dependencies.Services;

/// <summary>
/// Fetches and caches the host access token through the client credentials flow. Registered as a
/// singleton so the token is reused until shortly before it expires, instead of one request per
/// outgoing call.
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ServiceAuthenticationOptions> options,
    ILogger<ClientCredentialsTokenProvider> logger) : IServiceTokenProvider
{
    public const string HttpClientName = "erp-identity-token-client";

    private readonly ServiceAuthenticationOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _renewAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _renewAt)
            return _accessToken;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have renewed it while this one waited.
            if (_accessToken is not null && DateTimeOffset.UtcNow < _renewAt)
                return _accessToken;

            var token = await RequestTokenAsync(cancellationToken);

            _accessToken = token.AccessToken;
            _renewAt = DateTimeOffset.UtcNow
                .AddSeconds(Math.Max(1, token.ExpiresIn - _options.RenewBeforeExpirySeconds));

            logger.LogInformation(
                "Obtained a service access token for client {ClientId}, valid for {ExpiresIn}s.",
                _options.ClientId,
                token.ExpiresIn);

            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException(
                $"{ServiceAuthenticationOptions.SectionName}:ClientId and ClientSecret must be configured to call other services.");
        }

        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.ResolveTokenEndpoint())
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"The token endpoint refused the service credentials ({(int)response.StatusCode}): {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("The token endpoint returned no access token.");

        return token;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
