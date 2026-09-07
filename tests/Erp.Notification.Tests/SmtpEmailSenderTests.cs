using Erp.Notification.Application.Configuration;
using Erp.Notification.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Erp.Notification.Tests;

/// <summary>
/// Only the guard clauses and the cancellation path are covered here: the actual
/// delivery requires a live SMTP endpoint and is therefore out of scope for unit tests.
/// </summary>
public class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender(SmtpOptions options)
        => new(Options.Create(options));

    private static SmtpOptions ValidOptions() => new()
    {
        Host = "smtp.erp.local",
        Port = 587,
        FromEmail = "no-reply@erp.local",
        FromName = "ERP"
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_throws_when_the_host_is_not_configured(string? host)
    {
        var options = ValidOptions();
        options.Host = host!;
        var sender = CreateSender(options);

        var act = () => sender.SendAsync("ana@erp.local", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Smtp:Host is not configured.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_throws_when_the_from_email_is_not_configured(string? fromEmail)
    {
        var options = ValidOptions();
        options.FromEmail = fromEmail!;
        var sender = CreateSender(options);

        var act = () => sender.SendAsync("ana@erp.local", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Smtp:FromEmail is not configured.");
    }

    [Fact]
    public async Task SendAsync_validates_the_host_before_the_from_email()
    {
        var sender = CreateSender(new SmtpOptions());

        var act = () => sender.SendAsync("ana@erp.local", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Smtp:Host is not configured.");
    }

    [Fact]
    public async Task SendAsync_honours_a_cancelled_token_before_contacting_the_server()
    {
        var sender = CreateSender(ValidOptions());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sender.SendAsync("ana@erp.local", "Subject", "<p>Body</p>", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SendAsync_throws_a_format_error_for_an_invalid_recipient()
    {
        var sender = CreateSender(ValidOptions());

        var act = () => sender.SendAsync("not-an-email", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task SendAsync_throws_a_format_error_for_an_invalid_sender_address()
    {
        var options = ValidOptions();
        options.FromEmail = "not-an-email";
        var sender = CreateSender(options);

        var act = () => sender.SendAsync("ana@erp.local", "Subject", "<p>Body</p>");

        await act.Should().ThrowAsync<FormatException>();
    }
}
