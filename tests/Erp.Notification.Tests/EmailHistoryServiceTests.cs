using Erp.Notification.Application.Services;
using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Notification.Tests;

public class EmailHistoryServiceTests
{
    private readonly IEmailNotificationStorage _storage = Substitute.For<IEmailNotificationStorage>();

    private EmailHistoryService CreateService() => new(_storage);

    private EmailNotification Given(EmailNotificationStatus status, string? lastError = "erro anterior")
    {
        var notification = new EmailNotification
        {
            Id = Guid.NewGuid(),
            ToEmail = "ana@empresa.pt",
            Subject = "Assunto",
            HtmlBody = "<p>Corpo</p>",
            Status = status,
            RetryCount = 2,
            LastError = lastError,
            ProcessedAtUtc = DateTime.UtcNow
        };

        _storage.GetByIdAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
        return notification;
    }

    [Fact]
    public async Task RequeueFailedAsync_puts_a_failed_email_back_as_pending()
    {
        var notification = Given(EmailNotificationStatus.Failed);

        var requeued = await CreateService().RequeueFailedAsync(notification.Id);

        requeued.Should().BeTrue();
        notification.Status.Should().Be(EmailNotificationStatus.Pending);
        notification.LastError.Should().BeNull();
        notification.ProcessedAtUtc.Should().BeNull();
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequeueFailedAsync_keeps_the_retry_count_so_the_history_stays_honest()
    {
        var notification = Given(EmailNotificationStatus.Failed);

        await CreateService().RequeueFailedAsync(notification.Id);

        notification.RetryCount.Should().Be(2);
    }

    [Theory]
    [InlineData(EmailNotificationStatus.Sent)]
    [InlineData(EmailNotificationStatus.Pending)]
    public async Task RequeueFailedAsync_refuses_anything_that_did_not_fail(EmailNotificationStatus status)
    {
        var notification = Given(status);

        var requeued = await CreateService().RequeueFailedAsync(notification.Id);

        requeued.Should().BeFalse();
        notification.Status.Should().Be(status);
        await _storage.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequeueFailedAsync_is_false_for_an_unknown_email()
    {
        var requeued = await CreateService().RequeueFailedAsync(Guid.NewGuid());

        requeued.Should().BeFalse();
    }

    [Fact]
    public async Task GetHistoryAsync_returns_what_the_storage_gives()
    {
        IReadOnlyList<EmailNotification> history = [new() { Id = Guid.NewGuid(), ToEmail = "a@b.pt" }];
        _storage.GetHistoryAsync(Arg.Any<CancellationToken>()).Returns(history);

        var result = await CreateService().GetHistoryAsync();

        result.Should().BeSameAs(history);
    }

    [Fact]
    public async Task GetByIdAsync_returns_what_the_storage_gives()
    {
        var notification = Given(EmailNotificationStatus.Sent);

        var result = await CreateService().GetByIdAsync(notification.Id);

        result.Should().BeSameAs(notification);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_an_unknown_email()
    {
        var result = await CreateService().GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
