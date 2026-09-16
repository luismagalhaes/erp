using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.Models;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

using EntityClient = Duende.IdentityServer.EntityFramework.Entities.Client;

public sealed class ClientStorage(IDbContextFactory<ConfigurationDbContext> dbContextFactory) : IClientStorage
{
    public async Task<IReadOnlyList<ClientListItem>> GetClientsAsync(CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await configurationDbContext.Clients
            .Include(c => c.RedirectUris)
            .Include(c => c.AllowedScopes)
            .OrderBy(c => c.ClientId)
            .Select(c => new ClientListItem(
                c.ClientId,
                c.ClientName ?? string.Empty,
                c.Enabled,
                c.RedirectUris.OrderBy(r => r.RedirectUri).Select(r => r.RedirectUri).ToList(),
                c.AllowedScopes.OrderBy(s => s.Scope).Select(s => s.Scope).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientEditItem?> GetClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var client = await configurationDbContext.Clients
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.ClientSecrets)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client is null)
            return null;

        return new ClientEditItem(
            client.ClientId,
            client.ClientName ?? string.Empty,
            client.Enabled,
            client.RequirePkce,
            client.RequireClientSecret,
            client.AllowOfflineAccess,
            client.AccessTokenLifetime,
            client.SlidingRefreshTokenLifetime,
            client.AbsoluteRefreshTokenLifetime,
            ToRefreshTokenUsageName(client.RefreshTokenUsage),
            ToRefreshTokenExpirationName(client.RefreshTokenExpiration),
            (client.AllowedGrantTypes ?? [])
                .OrderBy(g => g.GrantType)
                .Select(g => g.GrantType)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .ToList(),
            (client.RedirectUris ?? [])
                .OrderBy(r => r.RedirectUri)
                .Select(r => r.RedirectUri)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList(),
            (client.PostLogoutRedirectUris ?? [])
                .OrderBy(r => r.PostLogoutRedirectUri)
                .Select(r => r.PostLogoutRedirectUri)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList(),
            (client.AllowedCorsOrigins ?? [])
                .OrderBy(c => c.Origin)
                .Select(c => c.Origin)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList(),
            (client.AllowedScopes ?? [])
                .OrderBy(s => s.Scope)
                .Select(s => s.Scope)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList(),
            (client.ClientSecrets ?? [])
                .OrderBy(s => s.Id)
                .Select(s => new ClientSecretItem(s.Value ?? string.Empty, s.Description, true))
                .Where(s => !string.IsNullOrWhiteSpace(s.Value))
                .ToList());
    }

    public async Task CreateClientAsync(ClientUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await configurationDbContext.Clients
            .AnyAsync(c => c.ClientId == request.ClientId, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"Client '{request.ClientId}' already exists.");

        var client = new EntityClient
        {
            ClientId = request.ClientId,
            ClientName = request.ClientName,
            Enabled = request.Enabled,
            RequirePkce = request.RequirePkce,
            RequireClientSecret = request.RequireClientSecret,
            AllowOfflineAccess = request.AllowOfflineAccess,
            AccessTokenLifetime = request.AccessTokenLifetime,
            SlidingRefreshTokenLifetime = request.SlidingRefreshTokenLifetime,
            AbsoluteRefreshTokenLifetime = request.AbsoluteRefreshTokenLifetime,
            RefreshTokenUsage = ParseRefreshTokenUsage(request.RefreshTokenUsage),
            RefreshTokenExpiration = ParseRefreshTokenExpiration(request.RefreshTokenExpiration),
            RedirectUris = [],
            PostLogoutRedirectUris = [],
            AllowedCorsOrigins = [],
            AllowedGrantTypes = [],
            AllowedScopes = [],
            ClientSecrets = []
        };

        AddCollectionItems(client, request);

        configurationDbContext.Clients.Add(client);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateClientAsync(string clientId, ClientUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var client = await configurationDbContext.Clients
            .Include(c => c.RedirectUris)
            .Include(c => c.PostLogoutRedirectUris)
            .Include(c => c.AllowedCorsOrigins)
            .Include(c => c.AllowedGrantTypes)
            .Include(c => c.AllowedScopes)
            .Include(c => c.ClientSecrets)
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client is null)
            throw new InvalidOperationException($"Client '{clientId}' was not found.");

        client.ClientName = request.ClientName;
        client.Enabled = request.Enabled;
        client.RequirePkce = request.RequirePkce;
        client.RequireClientSecret = request.RequireClientSecret;
        client.AllowOfflineAccess = request.AllowOfflineAccess;
        client.AccessTokenLifetime = request.AccessTokenLifetime;
        client.SlidingRefreshTokenLifetime = request.SlidingRefreshTokenLifetime;
        client.AbsoluteRefreshTokenLifetime = request.AbsoluteRefreshTokenLifetime;
        client.RefreshTokenUsage = ParseRefreshTokenUsage(request.RefreshTokenUsage);
        client.RefreshTokenExpiration = ParseRefreshTokenExpiration(request.RefreshTokenExpiration);

        client.RedirectUris ??= [];
        client.PostLogoutRedirectUris ??= [];
        client.AllowedCorsOrigins ??= [];
        client.AllowedGrantTypes ??= [];
        client.AllowedScopes ??= [];
        client.ClientSecrets ??= [];

        ReplaceCollection(
            client.RedirectUris,
            request.RedirectUris,
            value => new ClientRedirectUri { RedirectUri = value });

        ReplaceCollection(
            client.PostLogoutRedirectUris,
            request.PostLogoutRedirectUris,
            value => new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = value });

        ReplaceCollection(
            client.AllowedCorsOrigins,
            request.AllowedCorsOrigins,
            value => new ClientCorsOrigin { Origin = value });

        ReplaceCollection(
            client.AllowedGrantTypes,
            request.AllowedGrantTypes,
            value => new ClientGrantType { GrantType = value });

        ReplaceCollection(
            client.AllowedScopes,
            request.AllowedScopes,
            value => new ClientScope { Scope = value });

        ReplaceCollection(
            client.ClientSecrets,
            request.ClientSecrets,
            value => new ClientSecret
            {
                Type = "SharedSecret",
                Value = value.IsHashed ? value.Value : HashSecret(value.Value),
                Description = string.IsNullOrWhiteSpace(value.Description) ? null : value.Description.Trim()
            });

        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteClientAsync(string clientId, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var client = await configurationDbContext.Clients
            .FirstOrDefaultAsync(c => c.ClientId == clientId, cancellationToken);

        if (client is null)
            return;

        configurationDbContext.Clients.Remove(client);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    private static int ParseRefreshTokenUsage(string value)
    {
        return string.Equals(value, "OneTimeOnly", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static int ParseRefreshTokenExpiration(string value)
    {
        return string.Equals(value, "Absolute", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    private static string ToRefreshTokenUsageName(int value) => value == 1 ? "OneTimeOnly" : "ReUse";

    private static string ToRefreshTokenExpirationName(int value) => value == 1 ? "Absolute" : "Sliding";

    private static void AddCollectionItems(EntityClient client, ClientUpsertRequest request)
    {
        foreach (var redirectUri in request.RedirectUris)
            client.RedirectUris.Add(new ClientRedirectUri { RedirectUri = redirectUri });

        foreach (var postLogoutRedirectUri in request.PostLogoutRedirectUris)
            client.PostLogoutRedirectUris.Add(new ClientPostLogoutRedirectUri { PostLogoutRedirectUri = postLogoutRedirectUri });

        foreach (var origin in request.AllowedCorsOrigins)
            client.AllowedCorsOrigins.Add(new ClientCorsOrigin { Origin = origin });

        foreach (var grantType in request.AllowedGrantTypes)
            client.AllowedGrantTypes.Add(new ClientGrantType { GrantType = grantType });

        foreach (var scope in request.AllowedScopes)
            client.AllowedScopes.Add(new ClientScope { Scope = scope });

        foreach (var secret in request.ClientSecrets)
        {
            client.ClientSecrets.Add(new ClientSecret
            {
                Type = "SharedSecret",
                Value = secret.IsHashed ? secret.Value : HashSecret(secret.Value),
                Description = string.IsNullOrWhiteSpace(secret.Description) ? null : secret.Description.Trim()
            });
        }
    }

    private static void ReplaceCollection<TItem, TValue>(
        ICollection<TItem> target,
        IReadOnlyList<TValue> values,
        Func<TValue, TItem> factory)
        where TItem : class
    {
        target.Clear();

        foreach (var value in values)
            target.Add(factory(value));
    }

    private static string HashSecret(string plainText) => plainText.Sha256();
}
