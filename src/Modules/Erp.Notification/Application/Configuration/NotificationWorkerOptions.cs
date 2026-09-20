namespace Erp.Notification.Application.Configuration;

public sealed class NotificationWorkerOptions
{
    public int BatchSize { get; set; } = 20;
    public int PollingIntervalSeconds { get; set; } = 15;

    /// <summary>
    /// How many times a failed email is attempted in total before it is left alone. Without a cap,
    /// a permanently bad address or a down SMTP host gets retried forever, every polling cycle.
    /// </summary>
    public int MaxRetries { get; set; } = 5;
}
