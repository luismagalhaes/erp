using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class RoleStorage(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IRoleStorage
{
    public async Task<IReadOnlyList<RoleListItem>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Roles
            .OrderBy(x => x.Name)
            .Select(x => new RoleListItem(x.Name ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoleEditItem?> GetRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

        return role is null ? null : new RoleEditItem(role.Name ?? string.Empty);
    }

    public async Task CreateRoleAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var name = request.Name.Trim();
        var normalizedName = name.ToUpperInvariant();
        var exists = await dbContext.Roles.AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"Role '{name}' already exists.");

        dbContext.Roles.Add(new IdentityRole
        {
            Name = name,
            NormalizedName = normalizedName,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRoleAsync(string roleName, RoleUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken)
            ?? throw new InvalidOperationException($"Role '{roleName}' was not found.");

        var newName = request.Name.Trim();
        var normalizedName = newName.ToUpperInvariant();
        if (!string.Equals(roleName, newName, StringComparison.OrdinalIgnoreCase) &&
            await dbContext.Roles.AnyAsync(x => x.NormalizedName == normalizedName, cancellationToken))
        {
            throw new InvalidOperationException($"Role '{newName}' already exists.");
        }

        role.Name = newName;
        role.NormalizedName = normalizedName;
        role.ConcurrencyStamp = Guid.NewGuid().ToString();

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var role = await dbContext.Roles
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);

        if (role is null)
            return;

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
