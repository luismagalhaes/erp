using Erp.Identity.Data;
using Erp.Identity.Infrastructure.Storage;
using Erp.Identity.Storage.Configuration;
using Erp.Identity.Storage.Services;
using Erp.Identity.Storage.Storage;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddDbContextFactory<ApplicationDbContext>(
            options => options.UseSqlServer(connectionString),
            ServiceLifetime.Scoped);

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;

                // A signed-up account cannot sign in until the confirmation email's link is
                // clicked — SignUp.razor sends it, ConfirmEmail.razor is what turns this off for
                // that one account. PasswordSignInAsync then returns SignInResult.NotAllowed for
                // an unconfirmed account instead of authenticating it.
                options.SignIn.RequireConfirmedAccount = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/SignIn";
            options.AccessDeniedPath = "/Account/SignIn";
        });

        services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.EmitStaticAudienceClaim = true;
            })
            .AddConfigurationStore(options =>
                options.ConfigureDbContext = b =>
                    b.UseSqlServer(connectionString,
                        sql => sql.MigrationsAssembly("Erp.Identity.Storage")))
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlServer(connectionString,
                        sql => sql.MigrationsAssembly("Erp.Identity.Storage"));
                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600;
            })
            .AddAspNetIdentity<ApplicationUser>();

        // Overrides the default AspNetIdentity profile service so the "locale" claim (part of
        // the standard "profile" scope) is issued from ApplicationUser.PreferredLanguage.
        services.AddTransient<IProfileService, LocalizedProfileService>();

        services.AddDbContextFactory<ConfigurationDbContext>(
            options => options.UseSqlServer(connectionString,
                sql => sql.MigrationsAssembly("Erp.Identity.Storage")),
            ServiceLifetime.Scoped);

        services.Configure<AdminUserSeedOptions>(configuration.GetSection(AdminUserSeedOptions.SectionName));

        services.AddScoped<IUserStorage, UserStorage>();
        services.AddScoped<IClientStorage, ClientStorage>();
        services.AddScoped<IRoleStorage, RoleStorage>();
        services.AddScoped<IApiScopeStorage, ApiScopeStorage>();
        services.AddScoped<IApiResourceStorage, ApiResourceStorage>();
        services.AddScoped<IIdentityResourceStorage, IdentityResourceStorage>();
        services.AddScoped<IIdentityProviderStorage, IdentityProviderStorage>();
        services.AddScoped<ILoginAuditStorage, LoginAuditStorage>();

        return services;
    }
}
