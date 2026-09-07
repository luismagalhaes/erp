using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Entities;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class ApiScopeStorage(IDbContextFactory<ConfigurationDbContext> dbContextFactory) : IApiScopeStorage
{
    public async Task<IReadOnlyList<ApiScopeListItem>> GetApiScopesAsync(CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await configurationDbContext.ApiScopes
            .Include(x => x.UserClaims)
            .OrderBy(x => x.Name)
            .Select(x => new ApiScopeListItem(
                x.Name,
                x.DisplayName ?? string.Empty,
                x.Enabled,
                x.UserClaims.OrderBy(c => c.Type).Select(c => c.Type).ToList()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApiScopeEditItem?> GetApiScopeAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiScopes
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        return entity is null
            ? null
            : new ApiScopeEditItem(
                entity.Name,
                entity.DisplayName ?? string.Empty,
                entity.Description ?? string.Empty,
                entity.Enabled,
                entity.Required,
                entity.Emphasize,
                entity.ShowInDiscoveryDocument,
                entity.UserClaims.OrderBy(c => c.Type).Select(c => c.Type).ToList());
    }

    public async Task CreateApiScopeAsync(ApiScopeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();
        if (await configurationDbContext.ApiScopes.AnyAsync(x => x.Name == name, cancellationToken))
            throw new InvalidOperationException($"API Scope '{name}' already exists.");

        var entity = new ApiScope
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

        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new ApiScopeClaim { Type = x });

        configurationDbContext.ApiScopes.Add(entity);
        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateApiScopeAsync(string name, ApiScopeUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiScopes
            .Include(x => x.UserClaims)
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken)
            ?? throw new InvalidOperationException($"API Scope '{name}' was not found.");

        var newName = request.Name.Trim();
        if (!string.Equals(name, newName, StringComparison.OrdinalIgnoreCase) &&
            await configurationDbContext.ApiScopes.AnyAsync(x => x.Name == newName, cancellationToken))
        {
            throw new InvalidOperationException($"API Scope '{newName}' already exists.");
        }

        entity.Name = newName;
        entity.DisplayName = ToNullable(request.DisplayName);
        entity.Description = ToNullable(request.Description);
        entity.Enabled = request.Enabled;
        entity.Required = request.Required;
        entity.Emphasize = request.Emphasize;
        entity.ShowInDiscoveryDocument = request.ShowInDiscoveryDocument;

        entity.UserClaims ??= [];

        ReplaceCollection(entity.UserClaims, request.UserClaims, x => new ApiScopeClaim { Type = x });

        await configurationDbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteApiScopeAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var configurationDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await configurationDbContext.ApiScopes
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

        if (entity is null)
            return;

        configurationDbContext.ApiScopes.Remove(entity);
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
