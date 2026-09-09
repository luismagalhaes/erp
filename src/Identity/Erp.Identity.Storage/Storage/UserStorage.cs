using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Storage.Storage;

public sealed class UserStorage(IDbContextFactory<ApplicationDbContext> dbContextFactory) : IUserStorage
{
    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var users = await dbContext.Users
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(x => x.Id).ToList();
        var roleAssignments = await (
            from userRole in dbContext.Set<IdentityUserRole<string>>()
            join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, RoleName = role.Name ?? string.Empty })
            .ToListAsync(cancellationToken);

        var rolesByUserId = roleAssignments
            .GroupBy(x => x.UserId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x
                    .Select(y => y.RoleName)
                    .Where(y => !string.IsNullOrWhiteSpace(y))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var result = new List<UserListItem>(users.Count);

        foreach (var user in users)
        {
            result.Add(new UserListItem(
                user.Id,
                user.Email ?? string.Empty,
                user.FullName,
                user.IsActive,
                user.CreatedAt,
                rolesByUserId.TryGetValue(user.Id, out var roles) ? roles.ToList() : [],
                user.PreferredLanguage));
        }

        return result;
    }

    public async Task<UserEditItem?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            return null;

        var roles = await (
            from userRole in dbContext.Set<IdentityUserRole<string>>()
            join role in dbContext.Set<IdentityRole>() on userRole.RoleId equals role.Id
            where userRole.UserId == userId
            orderby role.Name
            select role.Name ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToListAsync(cancellationToken);

        return new UserEditItem(
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            user.IsActive,
            roles,
            user.PreferredLanguage);
    }

    public async Task UpdateUserLanguageAsync(string userId, string preferredLanguage, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.PreferredLanguage = preferredLanguage;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserRolesAsync(string userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var normalized = roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rolesByName = await dbContext.Set<IdentityRole>()
            .Where(x => normalized.Contains(x.Name!))
            .Select(x => new { Name = x.Name!, x.Id })
            .ToListAsync(cancellationToken);

        if (rolesByName.Count != normalized.Count)
        {
            var existingNames = rolesByName.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = normalized.Where(x => !existingNames.Contains(x));
            throw new InvalidOperationException($"Role(s) not found: {string.Join(", ", missing)}.");
        }

        var targetRoleIds = rolesByName.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentUserRoles = await dbContext.Set<IdentityUserRole<string>>()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var currentRoleIds = currentUserRoles
            .Select(x => x.RoleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var removeLinks = currentUserRoles
            .Where(x => !targetRoleIds.Contains(x.RoleId))
            .ToList();

        if (removeLinks.Count > 0)
            dbContext.RemoveRange(removeLinks);

        var addRoleIds = targetRoleIds
            .Where(x => !currentRoleIds.Contains(x))
            .ToList();

        if (addRoleIds.Count > 0)
        {
            dbContext.AddRange(addRoleIds.Select(roleId => new IdentityUserRole<string>
            {
                UserId = user.Id,
                RoleId = roleId
            }));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
