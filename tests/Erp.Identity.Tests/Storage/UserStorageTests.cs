using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Tests.Storage;

public class UserStorageTests
{
    private readonly InMemoryApplicationDbContextFactory _factory = new();

    private async Task<ApplicationUser> SeedUserAsync(string email, string firstName = "Ana", string lastName = "Silva")
    {
        await using var dbContext = _factory.CreateDbContext();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    private async Task<IdentityRole> SeedRoleAsync(string name)
    {
        await using var dbContext = _factory.CreateDbContext();

        var role = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            NormalizedName = name.ToUpperInvariant()
        };

        dbContext.Roles.Add(role);
        await dbContext.SaveChangesAsync();

        return role;
    }

    private async Task AssignRoleAsync(string userId, string roleId)
    {
        await using var dbContext = _factory.CreateDbContext();
        dbContext.Add(new IdentityUserRole<string> { UserId = userId, RoleId = roleId });
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetUsersAsync_returns_the_users_ordered_by_email_with_their_roles()
    {
        var storage = new UserStorage(_factory);
        var zeta = await SeedUserAsync("zeta@erp.local");
        await SeedUserAsync("alpha@erp.local");
        var role = await SeedRoleAsync("SuperAdmin");
        await AssignRoleAsync(zeta.Id, role.Id);

        var users = await storage.GetUsersAsync();

        users.Select(x => x.Email).Should().ContainInOrder("alpha@erp.local", "zeta@erp.local");
        users.Single(x => x.Email == "zeta@erp.local").Roles.Should().ContainSingle().Which.Should().Be("SuperAdmin");
        users.Single(x => x.Email == "alpha@erp.local").Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserAsync_returns_the_user_with_its_roles()
    {
        var storage = new UserStorage(_factory);
        var user = await SeedUserAsync("ana@erp.local");
        var role = await SeedRoleAsync("User");
        await AssignRoleAsync(user.Id, role.Id);

        var result = await storage.GetUserAsync(user.Id);

        result.Should().NotBeNull();
        result!.Email.Should().Be("ana@erp.local");
        result.FullName.Should().Be("Ana Silva");
        result.Roles.Should().ContainSingle().Which.Should().Be("User");
    }

    [Fact]
    public async Task GetUserAsync_returns_null_when_the_user_does_not_exist()
    {
        var storage = new UserStorage(_factory);

        (await storage.GetUserAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserRolesAsync_adds_and_removes_the_role_links()
    {
        var storage = new UserStorage(_factory);
        var user = await SeedUserAsync("ana@erp.local");
        var superAdmin = await SeedRoleAsync("SuperAdmin");
        await SeedRoleAsync("User");
        await AssignRoleAsync(user.Id, superAdmin.Id);

        await storage.UpdateUserRolesAsync(user.Id, [" User ", "user"]);

        var result = await storage.GetUserAsync(user.Id);
        result!.Roles.Should().ContainSingle().Which.Should().Be("User");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_clears_the_roles_when_an_empty_list_is_provided()
    {
        var storage = new UserStorage(_factory);
        var user = await SeedUserAsync("ana@erp.local");
        var role = await SeedRoleAsync("User");
        await AssignRoleAsync(user.Id, role.Id);

        await storage.UpdateUserRolesAsync(user.Id, []);

        var result = await storage.GetUserAsync(user.Id);
        result!.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateUserRolesAsync_throws_when_the_user_does_not_exist()
    {
        var storage = new UserStorage(_factory);

        var act = () => storage.UpdateUserRolesAsync("missing", ["User"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User not found.");
    }

    [Fact]
    public async Task UpdateUserRolesAsync_throws_when_a_role_does_not_exist()
    {
        var storage = new UserStorage(_factory);
        var user = await SeedUserAsync("ana@erp.local");

        var act = () => storage.UpdateUserRolesAsync(user.Id, ["Ghost"]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Ghost*");
    }
}
