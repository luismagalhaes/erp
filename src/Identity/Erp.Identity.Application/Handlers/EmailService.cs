using Erp.Identity.Infrastructure.Application;

namespace Erp.Identity.Application.Handlers;

public sealed class EmailService(INotificationEmailClient notificationEmailClient) : IEmailService
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => notificationEmailClient.QueueAsync(toEmail, subject, htmlBody, cancellationToken);
}
