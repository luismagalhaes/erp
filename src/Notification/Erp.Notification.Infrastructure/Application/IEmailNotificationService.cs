using Erp.Notification.Domain.Models;

namespace Erp.Notification.Infrastructure.Application;

public interface IEmailNotificationService
{
    Task<Guid> EnqueueAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default);
}
