using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class ApiResourceStorage(IDbContextFactory<ConfigurationDbContext> dbContextFactory) : IApiResourceStorage
{
    public async Task<IReadOnlyList<ApiResourceListItem>> GetApiResourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await configurationDbContext.ApiResources
            .Include(x => x.Scopes)
            .OrderBy(x => x.Name)
            .Select(x => new ApiResourceListItem(
                x.Name,
                x.DisplayName ?? string.Empty,
                x.Enabled,
                x.Scopes.OrderBy(s => s.Scope).Select(s => s.Scope).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiResourceEditItem?> GetApiResourceAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiResources
            .Include(x => x.Scopes)
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        return entity is null
            ? null
            : new ApiResourceEditItem(
                entity.Name,
                entity.DisplayName ?? string.Empty,
                entity.Description ?? string.Empty,
                entity.Enabled,
                entity.ShowInDiscoveryDocument,
                entity.RequireResourceIndicator,
                entity.Scopes.OrderBy(s => s.Scope).Select(s => s.Scope).ToList(),
                entity.UserClaims.OrderBy(c => c.Type).Select(c => c.Type).ToList());
    }

    public async Task CreateApiResourceAsync(ApiResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();
        if (await configurationDbContext.ApiResources.AnyAsync(x => x.Name == name, cancellationToken))
            throw new InvalidOperationException($"API Resource '{name}' already exists.");

        var entity = new ApiResource
        {
            Name = name,
            DisplayName = ToNullable(request.DisplayName),
            Description = ToNullable(request.Description),
            Enabled = request.Enabled,
            ShowInDiscoveryDocument = request.ShowInDiscoveryDocument,
            RequireResourceIndicator = request.RequireResourceIndicator,
            Scopes = [],
            UserClaims = []
        };

        ReplaceCollection(entity.Scopes, request.Scopes, x => new ApiResourceScope { Scope = x });
        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new ApiResourceClaim { Type = x });

        configurationDbContext.ApiResources.Add(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateApiResourceAsync(string name, ApiResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiResources
            .Include(x => x.Scopes)
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken)
            ?? throw new InvalidOperationException($"API Resource '{name}' was not found.");

        var newName = request.Name.Trim();
        if (!string.Equals(name, newName, StringComparison.OrdinalIgnoreCase) &&
            await configurationDbContext.ApiResources.AnyAsync(x => x.Name == newName, cancellationToken))
        {
            throw new InvalidOperationException($"API Resource '{newName}' already exists.");
        }

        entity.Name = newName;
        entity.DisplayName = ToNullable(request.DisplayName);
        entity.Description = ToNullable(request.Description);
        entity.Enabled = request.Enabled;
        entity.ShowInDiscoveryDocument = request.ShowInDiscoveryDocument;
        entity.RequireResourceIndicator = request.RequireResourceIndicator;

        entity.Scopes ??= [];
        entity.UserClaims ??= [];

        ReplaceCollection(entity.Scopes, request.Scopes, x => new ApiResourceScope { Scope = x });
        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new ApiResourceClaim { Type = x });

        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteApiResourceAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiResources
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (entity is null)
            return;

        configurationDbContext.ApiResources.Remove(entity);
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
