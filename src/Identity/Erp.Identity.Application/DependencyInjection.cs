using Erp.Identity.Application.Handlers;
using Erp.Identity.Infrastructure.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IApiScopeService, ApiScopeService>();
        services.AddScoped<IApiResourceService, ApiResourceService>();
        services.AddScoped<IIdentityResourceService, IdentityResourceService>();
        services.AddScoped<IIdentityProviderService, IdentityProviderService>();
        return services;
    }
}
