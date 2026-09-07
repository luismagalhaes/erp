using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class IdentityResourceStorage(IDbContextFactory<ConfigurationDbContext> dbContextFactory) : IIdentityResourceStorage
{
    public async Task<IReadOnlyList<IdentityResourceListItem>> GetIdentityResourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await configurationDbContext.IdentityResources
            .Include(x => x.UserClaims)
            .OrderBy(x => x.Name)
            .Select(x => new IdentityResourceListItem(
                x.Name,
                x.DisplayName ?? string.Empty,
                x.Enabled,
                x.UserClaims.OrderBy(c => c.Type).Select(c => c.Type).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IdentityResourceEditItem?> GetIdentityResourceAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityResources
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        return entity is null
            ? null
            : new IdentityResourceEditItem(
                entity.Name,
                entity.DisplayName ?? string.Empty,
                entity.Description ?? string.Empty,
                entity.Enabled,
                entity.Required,
                entity.Emphasize,
                entity.ShowInDiscoveryDocument,
                entity.UserClaims.OrderBy(c => c.Type).Select(c => c.Type).ToList());
    }

    public async Task CreateIdentityResourceAsync(IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();
        if (await configurationDbContext.IdentityResources.AnyAsync(x => x.Name == name, cancellationToken))
            throw new InvalidOperationException($"Identity Resource '{name}' already exists.");

        var entity = new IdentityResource
        {
            Name = name,
            DisplayName = ToNullable(request.DisplayName),
            Description = ToNullable(request.Description),
            Enabled = request.Enabled,
            Required = request.Required,
            Emphasize = request.Emphasize,
            ShowInDiscoveryDocument = request.ShowInDiscoveryDocument,
            UserClaims = []
        };

        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new IdentityResourceClaim { Type = x });

        configurationDbContext.IdentityResources.Add(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateIdentityResourceAsync(string name, IdentityResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityResources
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken)
            ?? throw new InvalidOperationException($"Identity Resource '{name}' was not found.");

        var newName = request.Name.Trim();
        if (!string.Equals(name, newName, StringComparison.OrdinalIgnoreCase) &&
            await configurationDbContext.IdentityResources.AnyAsync(x => x.Name == newName, cancellationToken))
        {
            throw new InvalidOperationException($"Identity Resource '{newName}' already exists.");
        }

        entity.Name = newName;
        entity.DisplayName = ToNullable(request.DisplayName);
        entity.Description = ToNullable(request.Description);
        entity.Enabled = request.Enabled;
        entity.Required = request.Required;
        entity.Emphasize = request.Emphasize;
        entity.ShowInDiscoveryDocument = request.ShowInDiscoveryDocument;

        entity.UserClaims ??= [];

        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new IdentityResourceClaim { Type = x });

        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteIdentityResourceAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.IdentityResources
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (entity is null)
            return;

        configurationDbContext.IdentityResources.Remove(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? ToNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ReplaceCollection<T>(ICollection<T> target, IEnumerable<string> values, Func<string, T> factory)
    {
        target.Clear();
        foreach (var value in values
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            target.Add(factory(value));
        }
    }
}
