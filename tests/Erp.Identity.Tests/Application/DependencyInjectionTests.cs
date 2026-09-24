using Erp.Identity.Application;
using Erp.Identity.Application.Handlers;
using Erp.Identity.Infrastructure.Application;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Tests.Application;

public class DependencyInjectionTests
{
    [Theory]
    [InlineData(typeof(IUserService), typeof(UserService))]
    [InlineData(typeof(IClientService), typeof(ClientService))]
    [InlineData(typeof(IRoleService), typeof(RoleService))]
    [InlineData(typeof(IEmailService), typeof(EmailService))]
    [InlineData(typeof(IApiScopeService), typeof(ApiScopeService))]
    [InlineData(typeof(IApiResourceService), typeof(ApiResourceService))]
    [InlineData(typeof(IIdentityResourceService), typeof(IdentityResourceService))]
    [InlineData(typeof(IIdentityProviderService), typeof(IdentityProviderService))]
    [InlineData(typeof(IOnboardingRequestService), typeof(OnboardingRequestService))]
    public void AddIdentityApplication_registers_every_service_as_scoped(Type serviceType, Type implementationType)
    {
        var services = new ServiceCollection();

        services.AddIdentityApplication();

        var descriptor = services.Should().ContainSingle(x => x.ServiceType == serviceType).Subject;
        descriptor.ImplementationType.Should().Be(implementationType);
        descriptor.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddIdentityApplication_returns_the_same_collection_for_chaining()
    {
        var services = new ServiceCollection();

        services.AddIdentityApplication().Should().BeSameAs(services);
    }
}
