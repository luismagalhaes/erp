using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Storage;

namespace Erp.Notification.Application.Services;

public sealed class EmailNotificationService(IEmailNotificationStorage storage) : IEmailNotificationService
{
    public async Task<Guid> EnqueueAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var notification = new EmailNotification
        {
            Id = Guid.NewGuid(),
            ToEmail = request.ToEmail.Trim(),
            Subject = request.Subject.Trim(),
            HtmlBody = request.HtmlBody,
            Status = EmailNotificationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        await storage.AddAsync(notification, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);
        return notification.Id;
    }
}
