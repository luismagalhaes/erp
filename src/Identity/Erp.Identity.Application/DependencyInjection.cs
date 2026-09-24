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
        services.AddScoped<ILoginAuditService, LoginAuditService>();
        services.AddScoped<IOnboardingRequestService, OnboardingRequestService>();
        // Scoped, not Singleton: now backed by ISignUpAttemptStorage (EF Core, itself Scoped) —
        // it stopped being an in-memory cache once the count needed to survive across instances.
        services.AddScoped<ISignUpAttemptTracker, SignUpAttemptTracker>();
        return services;
    }
}
