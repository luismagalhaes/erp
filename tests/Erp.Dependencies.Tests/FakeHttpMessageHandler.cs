using System.Net;
using System.Text;

namespace Erp.Dependencies.Tests;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> for the typed HttpClients in <c>Erp.Dependencies</c>:
/// hands back one canned response per call, in order, and records every request it saw.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string? Body)> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler Returns(HttpStatusCode status, string? body = null)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    /// <summary>Throws as if the network itself failed — no status code at all.</summary>
    public Exception? ThrowOnNextRequest { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (ThrowOnNextRequest is { } exception)
        {
            ThrowOnNextRequest = null;
            throw exception;
        }

        if (_responses.Count == 0)
            throw new InvalidOperationException("The fake handler was called more times than it was scripted for.");

        var (status, body) = _responses.Dequeue();

        var response = new HttpResponseMessage(status);

        if (body is not null)
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");

        return Task.FromResult(response);
    }
}
