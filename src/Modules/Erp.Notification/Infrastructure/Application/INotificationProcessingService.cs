namespace Erp.Notification.Infrastructure.Application;

public interface INotificationProcessingService
{
    /// <param name="maxRetries">A failed email stops being retried once it reaches this many attempts.</param>
    Task<int> ProcessPendingAsync(int batchSize, int maxRetries, CancellationToken cancellationToken = default);
}
