using Erp.Notification.Application.Services;
using Erp.Notification.Domain.Models;
using Erp.Notification.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Notification.Tests;

public class EmailNotificationServiceTests
{
    private readonly IEmailNotificationStorage _storage = Substitute.For<IEmailNotificationStorage>();
    private readonly List<EmailNotification> _persisted = [];

    public EmailNotificationServiceTests()
    {
        _storage
            .When(x => x.AddAsync(Arg.Any<EmailNotification>(), Arg.Any<CancellationToken>()))
            .Do(call => _persisted.Add(call.Arg<EmailNotification>()));
    }

    private EmailNotificationService CreateService() => new(_storage);

    private static EmailNotificationRequest Request(
        string toEmail = "  ana@empresa.pt  ",
        string subject = "  Recuperar password  ",
        string body = "<p>Olá</p>") =>
        new() { ToEmail = toEmail, Subject = subject, HtmlBody = body };

    [Fact]
    public async Task EnqueueAsync_stores_the_email_as_pending()
    {
        var id = await CreateService().EnqueueAsync(Request());

        var stored = _persisted.Should().ContainSingle().Subject;
        stored.Id.Should().Be(id);
        stored.Status.Should().Be(EmailNotificationStatus.Pending);
        stored.RetryCount.Should().Be(0);
        stored.ProcessedAtUtc.Should().BeNull();
        await _storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueAsync_trims_the_address_and_the_subject()
    {
        await CreateService().EnqueueAsync(Request());

        var stored = _persisted.Single();
        stored.ToEmail.Should().Be("ana@empresa.pt");
        stored.Subject.Should().Be("Recuperar password");
    }

    [Fact]
    public async Task EnqueueAsync_keeps_the_body_untouched()
    {
        const string body = "<p>Olá <strong>Ana</strong>,</p>  <p>link</p>";

        await CreateService().EnqueueAsync(Request(body: body));

        _persisted.Single().HtmlBody.Should().Be(body);
    }

    [Fact]
    public async Task EnqueueAsync_gives_every_email_its_own_identifier()
    {
        var service = CreateService();

        var first = await service.EnqueueAsync(Request());
        var second = await service.EnqueueAsync(Request());

        first.Should().NotBe(second);
        _persisted.Should().HaveCount(2);
    }
}
