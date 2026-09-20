using Erp.Notification.Application.Services;
using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Messaging;
using Erp.Notification.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Erp.Notification.Tests;

public class NotificationProcessingServiceTests
{
    private readonly IEmailNotificationStorage _storage = Substitute.For<IEmailNotificationStorage>();
    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();

    private NotificationProcessingService CreateService() =>
        new(_storage, _sender, NullLogger<NotificationProcessingService>.Instance);

    private static EmailNotification Pending(string toEmail = "ana@empresa.pt") => new()
    {
        Id = Guid.NewGuid(),
        ToEmail = toEmail,
        Subject = "Assunto",
        HtmlBody = "<p>Corpo</p>",
        Status = EmailNotificationStatus.Pending
    };

    private void GivenPending(params EmailNotification[] notifications) =>
        _storage.GetPendingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(notifications);

    [Fact]
    public async Task ProcessPendingAsync_marks_delivered_emails_as_sent()
    {
        var notification = Pending();
        GivenPending(notification);

        var processed = await CreateService().ProcessPendingAsync(10, 5);

        processed.Should().Be(1);
        notification.Status.Should().Be(EmailNotificationStatus.Sent);
        notification.ProcessedAtUtc.Should().NotBeNull();
        notification.LastError.Should().BeNull();
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingAsync_records_the_failure_instead_of_throwing()
    {
        var notification = Pending();
        GivenPending(notification);
        _sender.SendAsync(notification.ToEmail, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Smtp:Host is not configured."));

        var processed = await CreateService().ProcessPendingAsync(10, 5);

        processed.Should().Be(1);
        notification.Status.Should().Be(EmailNotificationStatus.Failed);
        notification.RetryCount.Should().Be(1);
        notification.LastError.Should().Be("Smtp:Host is not configured.");
        notification.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessPendingAsync_keeps_going_after_one_email_fails()
    {
        var failing = Pending("falha@empresa.pt");
        var succeeding = Pending("ok@empresa.pt");
        GivenPending(failing, succeeding);

        _sender.SendAsync("falha@empresa.pt", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("timeout"));

        var processed = await CreateService().ProcessPendingAsync(10, 5);

        processed.Should().Be(2);
        failing.Status.Should().Be(EmailNotificationStatus.Failed);
        succeeding.Status.Should().Be(EmailNotificationStatus.Sent);
    }

    [Fact]
    public async Task ProcessPendingAsync_counts_retries_across_cycles()
    {
        var notification = Pending();
        GivenPending(notification);
        _sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("timeout"));

        var service = CreateService();
        await service.ProcessPendingAsync(10, 5);
        await service.ProcessPendingAsync(10, 5);

        notification.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessPendingAsync_saves_nothing_new_when_the_queue_is_empty()
    {
        GivenPending();

        var processed = await CreateService().ProcessPendingAsync(10, 5);

        processed.Should().Be(0);
        await _sender.DidNotReceive().SendAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessPendingAsync_asks_the_storage_for_the_requested_batch_size()
    {
        GivenPending();

        await CreateService().ProcessPendingAsync(7, 5);

        await _storage.Received(1).GetPendingAsync(7, 5, Arg.Any<CancellationToken>());
    }

    /// <summary>The actual fix: a failed email must stop being retried once it hits the cap.</summary>
    [Fact]
    public async Task ProcessPendingAsync_asks_the_storage_for_the_configured_retry_cap()
    {
        GivenPending();

        await CreateService().ProcessPendingAsync(10, 3);

        await _storage.Received(1).GetPendingAsync(10, 3, Arg.Any<CancellationToken>());
    }
}
