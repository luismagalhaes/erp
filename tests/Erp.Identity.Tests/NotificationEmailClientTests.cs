using System.Net;
using System.Text.Json;
using Erp.Identity.Dependencies;
using Erp.Identity.Dependencies.Services;
using Erp.Identity.Infrastructure.Application;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Tests;

/// <summary>
/// The password reset page calls IEmailService, which ends up here. If this call silently
/// stopped working, users would never get their reset link, so the contract is pinned:
/// the right endpoint, the right payload, and a failure that does not pass unnoticed.
/// </summary>
public class NotificationEmailClientTests
{
    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.Accepted) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode);
        }
    }

    private static NotificationEmailClient CreateClient(RecordingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost:7117") });

    [Fact]
    public async Task QueueAsync_posts_to_the_notification_queue_endpoint()
    {
        var handler = new RecordingHandler();

        await CreateClient(handler).QueueAsync("ana@empresa.pt", "Reset your ERP password", "<p>link</p>");

        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.Should().Be("/api/notifications/email");
    }

    [Fact]
    public async Task QueueAsync_sends_the_recipient_subject_and_body()
    {
        var handler = new RecordingHandler();

        await CreateClient(handler).QueueAsync("ana@empresa.pt", "Reset your ERP password", "<p>link</p>");

        using var payload = JsonDocument.Parse(handler.Body!);
        payload.RootElement.GetProperty("toEmail").GetString().Should().Be("ana@empresa.pt");
        payload.RootElement.GetProperty("subject").GetString().Should().Be("Reset your ERP password");
        payload.RootElement.GetProperty("htmlBody").GetString().Should().Be("<p>link</p>");
    }

    [Fact]
    public async Task QueueAsync_fails_loudly_when_the_notification_service_refuses()
    {
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);

        var act = () => CreateClient(handler).QueueAsync("ana@empresa.pt", "Assunto", "<p>corpo</p>");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void AddIdentityDependencies_sends_the_internal_api_key_when_configured()
    {
        var client = BuildConfiguredClient(new Dictionary<string, string?>
        {
            ["NotificationService:BaseUrl"] = "https://localhost:7117",
            ["NotificationService:InternalApiKey"] = "chave-de-teste"
        });

        client.DefaultRequestHeaders.GetValues(DependencyInjection.InternalApiKeyHeader)
            .Should().ContainSingle().Which.Should().Be("chave-de-teste");
    }

    [Fact]
    public void AddIdentityDependencies_omits_the_key_header_when_it_is_not_configured()
    {
        var client = BuildConfiguredClient(new Dictionary<string, string?>
        {
            ["NotificationService:BaseUrl"] = "https://localhost:7117"
        });

        client.DefaultRequestHeaders.Contains(DependencyInjection.InternalApiKeyHeader).Should().BeFalse();
    }

    [Fact]
    public void AddIdentityDependencies_fails_when_the_base_url_is_missing()
    {
        var act = () => BuildConfiguredClient(new Dictionary<string, string?>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*BaseUrl*");
    }

    /// <summary>
    /// Builds the client through the real registration, so the configuration callback in
    /// AddIdentityDependencies is the one under test.
    /// </summary>
    private static HttpClient BuildConfiguredClient(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var provider = new ServiceCollection()
            .AddIdentityDependencies(configuration)
            .BuildServiceProvider();

        var client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(INotificationEmailClient));

        // Proves the named registration was found; otherwise the factory hands back a bare client.
        client.BaseAddress.Should().NotBeNull();

        return client;
    }
}
