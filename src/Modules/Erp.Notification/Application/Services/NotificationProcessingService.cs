using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Messaging;
using Erp.Notification.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Erp.Notification.Application.Services;

public sealed class NotificationProcessingService(
    IEmailNotificationStorage storage,
    IEmailSender emailSender,
    ILogger<NotificationProcessingService> logger) : INotificationProcessingService
{
    public async Task<int> ProcessPendingAsync(int batchSize, int maxRetries, CancellationToken cancellationToken = default)
    {
        var pending = await storage.GetPendingAsync(batchSize, maxRetries, cancellationToken);

        foreach (var notification in pending)
        {
            try
            {
                await emailSender.SendAsync(notification.ToEmail, notification.Subject, notification.HtmlBody, cancellationToken);
                notification.Status = EmailNotificationStatus.Sent;
                notification.LastError = null;
                notification.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send notification {NotificationId} to {ToEmail}.",
                    notification.Id,
                    notification.ToEmail);

                notification.Status = EmailNotificationStatus.Failed;
                notification.RetryCount++;
                notification.LastError = ex.InnerException?.Message ?? ex.Message;
                notification.ProcessedAtUtc = DateTime.UtcNow;

                if (notification.RetryCount >= maxRetries)
                {
                    logger.LogWarning(
                        "Notification {NotificationId} to {ToEmail} gave up after {RetryCount} attempts.",
                        notification.Id, notification.ToEmail, notification.RetryCount);
                }
            }
        }

        await storage.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
