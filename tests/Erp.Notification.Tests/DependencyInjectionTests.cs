using Erp.Notification.Application;
using Erp.Notification.Application.Configuration;
using Erp.Notification.Application.Services;
using Erp.Notification.Infrastructure.Application;
using Erp.Notification.Infrastructure.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Notification.Tests;

public class DependencyInjectionTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder().AddInMemoryCollection(values ?? []).Build();

    [Theory]
    [InlineData(typeof(IEmailNotificationService), typeof(EmailNotificationService))]
    [InlineData(typeof(IEmailHistoryService), typeof(EmailHistoryService))]
    [InlineData(typeof(INotificationProcessingService), typeof(NotificationProcessingService))]
    [InlineData(typeof(IEmailSender), typeof(SmtpEmailSender))]
    public void AddNotificationApplication_registers_every_service_as_scoped(Type serviceType, Type implementationType)
    {
        var services = new ServiceCollection();

        services.AddNotificationApplication(BuildConfiguration());

        var descriptor = services.Should().ContainSingle(x => x.ServiceType == serviceType).Subject;
        descriptor.ImplementationType.Should().Be(implementationType);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNotificationApplication_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddNotificationApplication(BuildConfiguration()).Should().BeSameAs(services);
    }

    [Fact]
    public void AddNotificationApplication_binds_the_smtp_section()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.erp.local",
            ["Smtp:Port"] = "2525",
            ["Smtp:UseSsl"] = "false",
            ["Smtp:FromEmail"] = "no-reply@erp.local"
        });

        services.AddNotificationApplication(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<SmtpOptions>>().Value;

        options.Host.Should().Be("smtp.erp.local");
        options.Port.Should().Be(2525);
        options.UseSsl.Should().BeFalse();
        options.FromEmail.Should().Be("no-reply@erp.local");
    }

    [Fact]
    public void AddNotificationApplication_binds_the_notification_worker_section()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["NotificationWorker:BatchSize"] = "50",
            ["NotificationWorker:PollingIntervalSeconds"] = "5"
        });

        services.AddNotificationApplication(configuration);

        var options = services.BuildServiceProvider().GetRequiredService<IOptions<NotificationWorkerOptions>>().Value;

        options.BatchSize.Should().Be(50);
        options.PollingIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void SmtpOptions_exposes_the_expected_defaults()
    {
        var options = new SmtpOptions();

        options.Host.Should().BeEmpty();
        options.Port.Should().Be(587);
        options.UseSsl.Should().BeTrue();
        options.UserName.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.FromEmail.Should().BeEmpty();
        options.FromName.Should().Be("ERP Notification");
    }

    [Fact]
    public void NotificationWorkerOptions_exposes_the_expected_defaults()
    {
        var options = new NotificationWorkerOptions();

        options.BatchSize.Should().Be(20);
        options.PollingIntervalSeconds.Should().Be(15);
    }
}
