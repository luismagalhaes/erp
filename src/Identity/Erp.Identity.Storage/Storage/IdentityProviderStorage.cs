using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class IdentityProviderStorage(IDbContextFactory<ConfigurationDbContext> dbContextFactory) : IIdentityProviderStorage
{
    public async Task<IReadOnlyList<IdentityProviderListItem>> GetIdentityProvidersAsync(CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await configurationDbContext.IdentityProviders
            .OrderBy(x => x.Scheme)
            .Select(x => new IdentityProviderListItem(
                x.Scheme,
                x.DisplayName ?? string.Empty,
                x.Type,
                x.Enabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<IdentityProviderEditItem?> GetIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityProviders
            .FirstOrDefaultAsync(x => x.Scheme == scheme, cancellationToken);

        return entity is null
            ? null
            : new IdentityProviderEditItem(
                entity.Scheme,
                entity.DisplayName ?? string.Empty,
                entity.Type,
                entity.Enabled);
    }

    public async Task CreateIdentityProviderAsync(IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var scheme = request.Scheme.Trim();
        if (await configurationDbContext.IdentityProviders.AnyAsync(x => x.Scheme == scheme, cancellationToken))
            throw new InvalidOperationException($"Identity Provider '{scheme}' already exists.");

        var entity = new IdentityProvider
        {
            Scheme = scheme,
            DisplayName = ToNullable(request.DisplayName),
            Type = request.Type.Trim(),
            Enabled = request.Enabled,
            Created = DateTime.UtcNow,
            Properties = "{}"
        };

        configurationDbContext.IdentityProviders.Add(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateIdentityProviderAsync(string scheme, IdentityProviderUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityProviders
            .FirstOrDefaultAsync(x => x.Scheme == scheme, cancellationToken)
            ?? throw new InvalidOperationException($"Identity Provider '{scheme}' was not found.");

        var newScheme = request.Scheme.Trim();
        if (!string.Equals(scheme, newScheme, StringComparison.OrdinalIgnoreCase) &&
            await configurationDbContext.IdentityProviders.AnyAsync(x => x.Scheme == newScheme, cancellationToken))
        {
            throw new InvalidOperationException($"Identity Provider '{newScheme}' already exists.");
        }

        entity.Scheme = newScheme;
        entity.DisplayName = ToNullable(request.DisplayName);
        entity.Type = request.Type.Trim();
        entity.Enabled = request.Enabled;
        entity.Updated = DateTime.UtcNow;

        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteIdentityProviderAsync(string scheme, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityProviders
            .FirstOrDefaultAsync(x => x.Scheme == scheme, cancellationToken);

        if (entity is null)
            return;

        configurationDbContext.IdentityProviders.Remove(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ToNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
