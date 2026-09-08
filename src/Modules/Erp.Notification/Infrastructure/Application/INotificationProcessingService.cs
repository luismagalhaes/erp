namespace Erp.Notification.Infrastructure.Application;

public interface INotificationProcessingService
{
    Task<int> ProcessPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}
