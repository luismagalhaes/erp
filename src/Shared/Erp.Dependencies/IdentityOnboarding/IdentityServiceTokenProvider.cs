using System.Net.Http.Json;
using Erp.Common;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Erp.Dependencies.IdentityOnboarding;

/// <summary>
/// Fetches and caches the access token this API uses when it calls the Identity host, so the token
/// is reused until shortly before it expires instead of one request per outgoing call.
/// </summary>
public sealed class IdentityServiceTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityServiceOptions> options,
    TimeProvider timeProvider)
{
    public const string HttpClientName = "erp-identity-service-token";

    /// <summary>How long before expiry a cached token is renewed.</summary>
    private const int RenewBeforeExpirySeconds = 60;

    private readonly IdentityServiceOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _renewAt = DateTimeOffset.MinValue;

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (IsCachedTokenUsable())
            return _accessToken!;

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have renewed it while this one waited.
            if (IsCachedTokenUsable())
                return _accessToken!;

            var token = await RequestTokenAsync(cancellationToken);

            _accessToken = token.AccessToken;
            _renewAt = timeProvider.GetUtcNow()
                .AddSeconds(Math.Max(1, token.ExpiresIn - RenewBeforeExpirySeconds));

            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsCachedTokenUsable() =>
        _accessToken is not null && timeProvider.GetUtcNow() < _renewAt;

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Authority)
            || string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException(
                $"{IdentityServiceOptions.SectionName}:Authority, ClientId and ClientSecret must be configured to call the Identity host.");
        }

        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Authority.TrimEnd('/')}/connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = Constants.Scopes.ErpIdentityOnboarding
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
