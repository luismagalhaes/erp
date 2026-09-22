using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Erp.Identity.Infrastructure.Application;
using Microsoft.Extensions.Configuration;

namespace Erp.Identity.Dependencies.Services;

public sealed class RecaptchaVerifier(HttpClient httpClient, IConfiguration configuration) : IRecaptchaVerifier
{
    /// <summary>Google's own recommended cut-off for v3 — 1.0 is certainly human, 0.0 certainly a bot.</summary>
    private const decimal MinimumScore = 0.5m;

    private string? SecretKey => configuration["Recaptcha:SecretKey"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, string expectedAction, CancellationToken cancellationToken = default)
    {
        var secretKey = SecretKey;
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
            return false;

        var parameters = new Dictionary<string, string>
        {
            ["secret"] = secretKey,
            ["response"] = token
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
            parameters["remoteip"] = remoteIp;

        using var response = await httpClient.PostAsync(
            "recaptcha/api/siteverify", new FormUrlEncodedContent(parameters), cancellationToken);

        if (!response.IsSuccessStatusCode)
            return false;

        var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken: cancellationToken);

        return result is { Success: true }
            && result.Score >= MinimumScore
            && string.Equals(result.Action, expectedAction, StringComparison.Ordinal);
    }

    private sealed class SiteVerifyResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public decimal Score { get; set; }

        [JsonPropertyName("action")]
        public string? Action { get; set; }
    }
}
