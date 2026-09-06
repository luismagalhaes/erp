namespace Erp.Identity.Infrastructure.Application;

public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
