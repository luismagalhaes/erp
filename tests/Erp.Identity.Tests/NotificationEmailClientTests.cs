using System.Net;
using System.Text.Json;
using Erp.Identity.Dependencies.Services;
using Erp.Identity.Infrastructure.Application;
using FluentAssertions;
using NSubstitute;

namespace Erp.Identity.Tests;

/// <summary>
/// The password reset page calls IEmailService, which ends up here. If this call silently
/// stopped working, users would never get their reset link, so the contract is pinned:
/// the right endpoint, the right payload, a bearer token, and a failure that is not swallowed.
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

    private static NotificationEmailClient CreateClient(HttpMessageHandler handler) =>
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
    public async Task The_call_carries_the_service_access_token()
    {
        var tokenProvider = Substitute.For<IServiceTokenProvider>();
        tokenProvider.GetAccessTokenAsync(Arg.Any<CancellationToken>()).Returns("token-de-servico");

        var recording = new RecordingHandler();
        var handler = new ServiceTokenHandler(tokenProvider) { InnerHandler = recording };

        await CreateClient(handler).QueueAsync("ana@empresa.pt", "Assunto", "<p>corpo</p>");

        recording.Request!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        recording.Request.Headers.Authorization.Parameter.Should().Be("token-de-servico");
    }
}
