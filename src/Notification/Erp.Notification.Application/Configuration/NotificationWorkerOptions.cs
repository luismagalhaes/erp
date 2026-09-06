namespace Erp.Notification.Application.Configuration;

public sealed class NotificationWorkerOptions
{
    public int BatchSize { get; set; } = 20;
    public int PollingIntervalSeconds { get; set; } = 15;
}
