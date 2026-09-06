using System.Net.Http.Json;
using Erp.Identity.Infrastructure.Application;

namespace Erp.Identity.Dependencies.Services;

public sealed class NotificationEmailClient(HttpClient httpClient) : INotificationEmailClient
{
    private const string EmailEndpoint = "/api/notifications/email";

    public async Task QueueAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var request = new EmailNotificationRequest
        {
            ToEmail = toEmail,
            Subject = subject,
            HtmlBody = htmlBody
        };

        using var response = await httpClient.PostAsJsonAsync(EmailEndpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed class EmailNotificationRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlBody { get; set; } = string.Empty;
    }
}
