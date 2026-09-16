using Erp.Notification.Application.Configuration;
using Erp.Notification.Infrastructure.Messaging;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MailAddress = System.Net.Mail.MailAddress;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace Erp.Notification.Application.Services;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions) : IEmailSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("Smtp:Host is not configured.");

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
            throw new InvalidOperationException("Smtp:FromEmail is not configured.");

        // MimeKit's MailboxAddress parses leniently and won't reject a malformed address the way
        // MailAddress does, so it's used here purely for the format check, not for sending.
        _ = new MailAddress(_options.FromEmail);
        _ = new MailAddress(toEmail);

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();

        var secureSocketOptions = _options.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
        await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
