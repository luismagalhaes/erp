using Erp.Notification.Application.Configuration;
using Erp.Notification.Infrastructure.Application;
using Microsoft.Extensions.Options;

namespace Erp.Notification.Worker;

/// <summary>
/// Drains the email queue on a fixed interval. Delivery failures are recorded on the
/// notification itself, so a broken SMTP host does not stop the loop.
/// </summary>
public sealed class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationWorkerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly NotificationWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));

        logger.LogInformation(
            "Notification worker started. Batch size {BatchSize}, polling every {Interval}.",
            _options.BatchSize,
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<INotificationProcessingService>();

                var processed = await processor.ProcessPendingAsync(_options.BatchSize, stoppingToken);

                if (processed > 0)
                    logger.LogInformation("Processed {Count} pending notifications.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification processing cycle failed; retrying on the next interval.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Notification worker stopped.");
    }
}
