using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Identity.Data
{
public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();

        var applicationDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configurationDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        var persistedGrantDbContext = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>();

        await applicationDbContext.Database.MigrateAsync();
        await configurationDbContext.Database.MigrateAsync();
        await persistedGrantDbContext.Database.MigrateAsync();

        var configuredClientIds = Clients.Select(c => c.ClientId).ToHashSet();
        var configuredIdentityResourceNames = IdentityResources.Select(r => r.Name).ToHashSet();
        var configuredApiScopeNames = ApiScopes.Select(s => s.Name).ToHashSet();
        var configuredApiResourceNames = ApiResources.Select(r => r.Name).ToHashSet();

        var existingClients = await configurationDbContext.Clients.Where(c => configuredClientIds.Contains(c.ClientId)).ToListAsync();
        if (existingClients.Count > 0)
            configurationDbContext.Clients.RemoveRange(existingClients);

        var existingIdentityResources = await configurationDbContext.IdentityResources.Where(r => configuredIdentityResourceNames.Contains(r.Name)).ToListAsync();
        if (existingIdentityResources.Count > 0)
            configurationDbContext.IdentityResources.RemoveRange(existingIdentityResources);

        var existingApiScopes = await configurationDbContext.ApiScopes.Where(s => configuredApiScopeNames.Contains(s.Name)).ToListAsync();
        if (existingApiScopes.Count > 0)
            configurationDbContext.ApiScopes.RemoveRange(existingApiScopes);

        var existingApiResources = await configurationDbContext.ApiResources.Where(r => configuredApiResourceNames.Contains(r.Name)).ToListAsync();
        if (existingApiResources.Count > 0)
            configurationDbContext.ApiResources.RemoveRange(existingApiResources);

        await configurationDbContext.SaveChangesAsync();

        configurationDbContext.Clients.AddRange(Clients.Select(x => x.ToEntity()));
        configurationDbContext.IdentityResources.AddRange(IdentityResources.Select(x => x.ToEntity()));
        configurationDbContext.ApiScopes.AddRange(ApiScopes.Select(x => x.ToEntity()));
        configurationDbContext.ApiResources.AddRange(ApiResources.Select(x => x.ToEntity()));
        await configurationDbContext.SaveChangesAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string[] roles = Constants.Roles.All;
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        const string adminEmail = Constants.AdminUser.Email;
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FirstName = Constants.AdminUser.FirstName,
                LastName = Constants.AdminUser.LastName,
                IsActive = true
            };

            var result = await userManager.CreateAsync(admin, Constants.AdminUser.Password);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, Constants.Roles.SuperAdmin);
        }
    }

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope(Constants.Scopes.ErpCoreRead,  Constants.ScopeDisplayNames.CoreRead),
        new ApiScope(Constants.Scopes.ErpCoreWrite, Constants.ScopeDisplayNames.CoreWrite),
        new ApiScope(Constants.Scopes.ErpSalesRead,  Constants.ScopeDisplayNames.SalesRead),
        new ApiScope(Constants.Scopes.ErpSalesWrite, Constants.ScopeDisplayNames.SalesWrite),
        new ApiScope(Constants.Scopes.ErpInventoryRead,  Constants.ScopeDisplayNames.InventoryRead),
        new ApiScope(Constants.Scopes.ErpInventoryWrite, Constants.ScopeDisplayNames.InventoryWrite),
        new ApiScope(Constants.Scopes.ErpPurchasingRead,  Constants.ScopeDisplayNames.PurchasingRead),
        new ApiScope(Constants.Scopes.ErpPurchasingWrite, Constants.ScopeDisplayNames.PurchasingWrite),
        new ApiScope(Constants.Scopes.ErpAccountingRead,  Constants.ScopeDisplayNames.AccountingRead),
        new ApiScope(Constants.Scopes.ErpAccountingWrite, Constants.ScopeDisplayNames.AccountingWrite),
        new ApiScope(Constants.Scopes.ErpReportingRead, Constants.ScopeDisplayNames.ReportingRead),
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new ApiResource(Constants.ApiResources.CoreApi, Constants.ApiResources.CoreApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpCoreRead, Constants.Scopes.ErpCoreWrite }
        },
        new ApiResource(Constants.ApiResources.SalesApi, Constants.ApiResources.SalesApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpSalesRead, Constants.Scopes.ErpSalesWrite }
        },
        new ApiResource(Constants.ApiResources.InventoryApi, Constants.ApiResources.InventoryApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpInventoryRead, Constants.Scopes.ErpInventoryWrite }
        },
        new ApiResource(Constants.ApiResources.PurchasingApi, Constants.ApiResources.PurchasingApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpPurchasingRead, Constants.Scopes.ErpPurchasingWrite }
        },
        new ApiResource(Constants.ApiResources.AccountingApi, Constants.ApiResources.AccountingApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpAccountingRead, Constants.Scopes.ErpAccountingWrite }
        },
        new ApiResource(Constants.ApiResources.ReportingApi, Constants.ApiResources.ReportingApiDisplayName)
        {
            Scopes = { Constants.Scopes.ErpReportingRead }
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
                Constants.Scopes.ErpCoreRead, Constants.Scopes.ErpCoreWrite,
                Constants.Scopes.ErpSalesRead, Constants.Scopes.ErpSalesWrite,
                Constants.Scopes.ErpInventoryRead, Constants.Scopes.ErpInventoryWrite,
                Constants.Scopes.ErpPurchasingRead, Constants.Scopes.ErpPurchasingWrite,
                Constants.Scopes.ErpAccountingRead, Constants.Scopes.ErpAccountingWrite,
                Constants.Scopes.ErpReportingRead
            },
            AllowOfflineAccess = true,
            AccessTokenLifetime = 3600,
            RefreshTokenUsage = TokenUsage.ReUse,
            RefreshTokenExpiration = TokenExpiration.Sliding,
            SlidingRefreshTokenLifetime = 86400,
        },
        new Client
        {
            ClientId = Constants.Clients.SalesServiceClientId,
            ClientName = Constants.Clients.SalesServiceClientName,
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret(Constants.Clients.SalesServiceSecret.Sha256()) },
            AllowedScopes = { Constants.Scopes.ErpCoreRead, Constants.Scopes.ErpInventoryRead, Constants.Scopes.ErpAccountingWrite }
        },
        new Client
        {
            ClientId = Constants.Clients.NotificationUiClientId,
            ClientName = Constants.Clients.NotificationUiClientName,
            AllowedGrantTypes = GrantTypes.Code,
            RequireClientSecret = false,
            RequirePkce = true,
            RedirectUris = { Constants.Clients.HttpsLocalhost7125 + Constants.Clients.NotificationUiLoginCallbackPath },
            PostLogoutRedirectUris = { Constants.Clients.HttpsLocalhost7125 + Constants.Clients.NotificationUiLogoutCallbackPath },
            AllowedScopes =
            {
                Constants.Scopes.OpenId,
                Constants.Scopes.Profile,
                Constants.Scopes.Email,
                Constants.Scopes.OfflineAccess
            },
            AllowOfflineAccess = true
        },
        new Client
        {
            ClientId = Constants.Clients.ReportingServiceClientId,
            ClientName = Constants.Clients.ReportingServiceClientName,
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret(Constants.Clients.ReportingServiceSecret.Sha256()) },
            AllowedScopes = { Constants.Scopes.ErpCoreRead, Constants.Scopes.ErpSalesRead, Constants.Scopes.ErpInventoryRead, Constants.Scopes.ErpAccountingRead }
        },
    ];
}
}
