using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Erp.Identity.Storage.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Erp.Identity.Data
{
public static class SeedData
{
    /// <summary>
    /// Applies pending migrations to the three Identity contexts. Separate from
    /// <see cref="InitializeAsync"/> so it can run on every startup, in every environment — unlike
    /// seeding, applying a migration is never destructive, and the schema has to be current before
    /// anything else touches these tables.
    /// </summary>
    public static async Task MigrateAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
    }

    /// <summary>Seeds the clients, scopes, resources, roles and admin user that don't already exist.
    /// Runs on every startup in every environment, because the app cannot function without them —
    /// but only *creates*, never touches a record that's already there, so editing a client (its
    /// redirect URIs, for instance) in the backoffice survives the next restart/deploy.</summary>
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var configurationDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        var existingClientIds = (await configurationDbContext.Clients.Select(c => c.ClientId).ToListAsync()).ToHashSet();
        var newClients = Clients.Where(c => !existingClientIds.Contains(c.ClientId));
        configurationDbContext.Clients.AddRange(newClients.Select(x => x.ToEntity()));

        var existingIdentityResourceNames = (await configurationDbContext.IdentityResources.Select(r => r.Name).ToListAsync()).ToHashSet();
        var newIdentityResources = IdentityResources.Where(r => !existingIdentityResourceNames.Contains(r.Name));
        configurationDbContext.IdentityResources.AddRange(newIdentityResources.Select(x => x.ToEntity()));

        var existingApiScopeNames = (await configurationDbContext.ApiScopes.Select(s => s.Name).ToListAsync()).ToHashSet();
        var newApiScopes = ApiScopes.Where(s => !existingApiScopeNames.Contains(s.Name));
        configurationDbContext.ApiScopes.AddRange(newApiScopes.Select(x => x.ToEntity()));

        var existingApiResourceNames = (await configurationDbContext.ApiResources.Select(r => r.Name).ToListAsync()).ToHashSet();
        var newApiResources = ApiResources.Where(r => !existingApiResourceNames.Contains(r.Name));
        configurationDbContext.ApiResources.AddRange(newApiResources.Select(x => x.ToEntity()));

        await configurationDbContext.SaveChangesAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = Constants.Roles.All;
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var adminUser = scope.ServiceProvider.GetRequiredService<IOptions<AdminUserSeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(adminUser.Email) || string.IsNullOrWhiteSpace(adminUser.Password))
            throw new InvalidOperationException(
                $"{AdminUserSeedOptions.SectionName}:Email and {AdminUserSeedOptions.SectionName}:Password are " +
                "not configured; cannot seed the SuperAdmin account.");

        if (await userManager.FindByEmailAsync(adminUser.Email) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminUser.Email,
                Email = adminUser.Email,
                EmailConfirmed = true,
                FirstName = adminUser.FirstName,
                LastName = adminUser.LastName,
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, adminUser.Password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, Constants.Roles.SuperAdmin);
            else
                Console.WriteLine("ADMIN SEED FAILED: " + string.Join("; ", result.Errors.Select(e => e.Code + ":" + e.Description)));
        }
    }

    /// <summary>
    /// The role claim needs an identity resource of its own: identity resources feed the id_token,
    /// and without one the UI principal carries no roles, so an AuthorizeView by role hides itself
    /// even from a SuperAdmin. The API is unaffected, it reads roles from the access token.
    /// </summary>
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource(
            Constants.Scopes.Roles,
            Constants.ScopeDisplayNames.Roles,
            [Constants.Claims.Role])
    ];

    /// <summary>
    /// One API means two levels of access instead of one pair per module. Only the capabilities
    /// that must never be granted to a signed in user keep a scope of their own.
    /// </summary>
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope(Constants.Scopes.ErpRead,  Constants.ScopeDisplayNames.ErpRead),
        new ApiScope(Constants.Scopes.ErpWrite, Constants.ScopeDisplayNames.ErpWrite),
        new ApiScope(Constants.Scopes.ErpNotificationSend, Constants.ScopeDisplayNames.NotificationSend),
        new ApiScope(Constants.Scopes.ErpIdentityRead,     Constants.ScopeDisplayNames.IdentityRead),
    ];

    /// <summary>
    /// Every API resource asks for the role claim: without it the access token carries no roles
    /// and the [Authorize(Roles = ...)] endpoints answer 403 even to a SuperAdmin.
    /// </summary>
    public static IEnumerable<ApiResource> ApiResources =>
    [
        // All business modules are served by one host, so one audience covers them. What
        // separates access between modules is the scope, not the resource.
        new ApiResource(Constants.ApiResources.ErpApi, Constants.ApiResources.ErpApiDisplayName)
        {
            Scopes =
            {
                Constants.Scopes.ErpRead,
                Constants.Scopes.ErpWrite,
                Constants.Scopes.ErpNotificationSend
            },
            UserClaims = { Constants.Claims.Role, Constants.Claims.Name }
        },
        // The Identity host also serves a read only users API, so the UI can assign users
        // to companies without duplicating the user store.
        new ApiResource(Constants.ApiResources.IdentityApi, Constants.ApiResources.IdentityApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpIdentityRead },
            UserClaims = { Constants.Claims.Role, Constants.Claims.Name }
        },
    ];

    public static IEnumerable<Client> Clients =>
    [
        new Client
        {
            ClientId = Constants.Clients.BlazorWasmClientId,
            ClientName = Constants.Clients.BlazorWasmClientName,
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequirePkce = true,
            AllowedCorsOrigins = { Constants.Clients.HttpsLocalhost7019, Constants.Clients.HttpLocalhost5191 },
            RedirectUris = { Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LoginCallbackPath, Constants.Clients.HttpLocalhost5191 + Constants.Clients.LoginCallbackPath },
            PostLogoutRedirectUris = { Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LogoutCallbackPath, Constants.Clients.HttpLocalhost5191 + Constants.Clients.LogoutCallbackPath },
            AllowedScopes =
            {
                Constants.Scopes.OpenId, Constants.Scopes.Profile, Constants.Scopes.Email, Constants.Scopes.OfflineAccess,
                Constants.Scopes.Roles,
                Constants.Scopes.ErpRead,
                Constants.Scopes.ErpWrite,
                Constants.Scopes.ErpIdentityRead
            },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 3600,
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 86400,
        },
        new Client
        {
            ClientId = Constants.Clients.IdentityServiceClientId,
            ClientName = Constants.Clients.IdentityServiceClientName,
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            // No secret is seeded here on purpose: the seed only creates, it never updates an
            // existing client, so a value baked in here would either be the same in every
            // environment or, once seeded, impossible to rotate by changing config. Set the
            // secret for this client from the backoffice after first deploy, one per environment.
            RequireClientSecret = true,
            AllowedScopes = { Constants.Scopes.ErpNotificationSend },
            AccessTokenLifetime = 3600
        },
    ];
}
}
