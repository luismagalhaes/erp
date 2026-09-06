using System.Net;
using System.Text.Json;
using Erp.Identity.Dependencies.Configuration;
using Erp.Identity.Dependencies.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Identity.Tests;

/// <summary>
/// The host calls the notification service with its own client credentials token instead of a
/// shared key, so the token has to be requested correctly, reused while valid, and renewed
/// before it expires.
/// </summary>
public class ClientCredentialsTokenProviderTests
{
    private sealed class TokenEndpointHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        int expiresIn = 3600) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public List<string> Bodies { get; } = [];
        public Uri? LastRequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestUri = request.RequestUri;
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

            if (statusCode != HttpStatusCode.OK)
                return new HttpResponseMessage(statusCode) { Content = new StringContent("{\"error\":\"invalid_client\"}") };

            var json = JsonSerializer.Serialize(new
            {
                access_token = $"token-{CallCount}",
                expires_in = expiresIn,
                token_type = "Bearer"
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>Lets the tests move time forward without sleeping.</summary>
    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }

    private static ClientCredentialsTokenProvider CreateProvider(
        TokenEndpointHandler handler,
        ServiceAuthenticationOptions? options = null,
        TimeProvider? clock = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(ClientCredentialsTokenProvider.HttpClientName)
            .Returns(_ => new HttpClient(handler, disposeHandler: false));

        options ??= new ServiceAuthenticationOptions
        {
            Authority = "https://localhost:7081",
            ClientId = "identity-service",
            ClientSecret = "segredo",
            Scope = "erp.notification.send"
        };

        return new ClientCredentialsTokenProvider(
            factory,
            Options.Create(options),
            clock ?? TimeProvider.System,
            NullLogger<ClientCredentialsTokenProvider>.Instance);
    }

    [Fact]
    public async Task GetAccessTokenAsync_requests_the_client_credentials_grant()
    {
        var handler = new TokenEndpointHandler();

        var token = await CreateProvider(handler).GetAccessTokenAsync();

        token.Should().Be("token-1");
        handler.LastRequestUri.Should().Be(new Uri("https://localhost:7081/connect/token"));

        var body = handler.Bodies.Single();
        body.Should().Contain("grant_type=client_credentials");
        body.Should().Contain("client_id=identity-service");
        body.Should().Contain("scope=erp.notification.send");
    }

    [Fact]
    public async Task GetAccessTokenAsync_reuses_the_token_while_it_is_valid()
    {
        var handler = new TokenEndpointHandler(expiresIn: 3600);
        var provider = CreateProvider(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        first.Should().Be(second);
        handler.CallCount.Should().Be(1, "a cached token avoids a round trip per email");
    }

    [Fact]
    public async Task GetAccessTokenAsync_renews_the_token_before_it_expires()
    {
        var handler = new TokenEndpointHandler(expiresIn: 3600);
        var clock = new TestClock(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(handler, clock: clock);

        var first = await provider.GetAccessTokenAsync();

        // Still inside the usable window: 3600s lifetime minus the 60s renewal margin.
        clock.Advance(TimeSpan.FromSeconds(3500));
        (await provider.GetAccessTokenAsync()).Should().Be(first);
        handler.CallCount.Should().Be(1);

        // Now inside the renewal margin, so the next call fetches a fresh token.
        clock.Advance(TimeSpan.FromSeconds(100));
        (await provider.GetAccessTokenAsync()).Should().Be("token-2");
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_still_caches_a_token_shorter_than_the_renewal_margin()
    {
        var handler = new TokenEndpointHandler(expiresIn: 30);
        var clock = new TestClock(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero));
        var provider = CreateProvider(
            handler,
            new ServiceAuthenticationOptions
            {
                Authority = "https://localhost:7081",
                ClientId = "identity-service",
                ClientSecret = "segredo",
                RenewBeforeExpirySeconds = 60
            },
            clock);

        await provider.GetAccessTokenAsync();
        await provider.GetAccessTokenAsync();

        handler.CallCount.Should().Be(1, "renewing on every call would hammer the token endpoint");

        clock.Advance(TimeSpan.FromSeconds(2));
        await provider.GetAccessTokenAsync();

        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_requests_one_token_when_called_concurrently()
    {
        var handler = new TokenEndpointHandler();
        var provider = CreateProvider(handler);

        var tokens = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => provider.GetAccessTokenAsync()));

        tokens.Should().AllBe("token-1");
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAccessTokenAsync_explains_a_refused_credential()
    {
        var handler = new TokenEndpointHandler(HttpStatusCode.BadRequest);

        var act = () => CreateProvider(handler).GetAccessTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*refused the service credentials*");
    }

    [Fact]
    public async Task GetAccessTokenAsync_fails_when_no_credentials_are_configured()
    {
        var handler = new TokenEndpointHandler();
        var provider = CreateProvider(handler, new ServiceAuthenticationOptions
        {
            Authority = "https://localhost:7081"
        });

        var act = () => provider.GetAccessTokenAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ClientId and ClientSecret*");
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public void ResolveTokenEndpoint_defaults_to_the_duende_token_endpoint()
    {
        var options = new ServiceAuthenticationOptions { Authority = "https://localhost:7081/" };

        options.ResolveTokenEndpoint().Should().Be(new Uri("https://localhost:7081/connect/token"));
    }

    [Fact]
    public void ResolveTokenEndpoint_honours_an_explicit_override()
    {
        var options = new ServiceAuthenticationOptions
        {
            Authority = "https://localhost:7081",
            TokenEndpoint = "https://sso.example.pt/oauth2/token"
        };

        options.ResolveTokenEndpoint().Should().Be(new Uri("https://sso.example.pt/oauth2/token"));
    }

    [Fact]
    public void ResolveTokenEndpoint_fails_without_an_authority()
    {
        var act = () => new ServiceAuthenticationOptions().ResolveTokenEndpoint();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Authority*");
    }
}
