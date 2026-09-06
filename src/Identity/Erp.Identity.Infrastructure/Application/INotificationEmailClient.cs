namespace Erp.Identity.Infrastructure.Application;

public interface INotificationEmailClient
{
    Task QueueAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
