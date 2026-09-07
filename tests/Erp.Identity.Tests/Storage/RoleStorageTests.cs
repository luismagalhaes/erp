using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class RoleStorageTests
{
    private readonly InMemoryApplicationDbContextFactory _factory = new();

    [Fact]
    public async Task CreateRoleAsync_trims_the_name_and_normalizes_it()
    {
        var storage = new RoleStorage(_factory);

        await storage.CreateRoleAsync(new RoleUpsertRequest("  SuperAdmin  "));

        var role = await storage.GetRoleAsync("SuperAdmin");

        role.Should().NotBeNull();
        role!.Name.Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task CreateRoleAsync_throws_when_the_role_already_exists_ignoring_case()
    {
        var storage = new RoleStorage(_factory);
        await storage.CreateRoleAsync(new RoleUpsertRequest("SuperAdmin"));

        var act = () => storage.CreateRoleAsync(new RoleUpsertRequest("superadmin"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task GetRolesAsync_returns_the_roles_ordered_by_name()
    {
        var storage = new RoleStorage(_factory);
        await storage.CreateRoleAsync(new RoleUpsertRequest("User"));
        await storage.CreateRoleAsync(new RoleUpsertRequest("SuperAdmin"));

        var roles = await storage.GetRolesAsync();

        roles.Select(x => x.Name).Should().ContainInOrder("SuperAdmin", "User");
    }

    [Fact]
    public async Task GetRoleAsync_returns_null_when_the_role_does_not_exist()
    {
        var storage = new RoleStorage(_factory);

        (await storage.GetRoleAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateRoleAsync_renames_the_role()
    {
        var storage = new RoleStorage(_factory);
        await storage.CreateRoleAsync(new RoleUpsertRequest("User"));

        await storage.UpdateRoleAsync("User", new RoleUpsertRequest(" Operator "));

        (await storage.GetRoleAsync("User")).Should().BeNull();
        (await storage.GetRoleAsync("Operator")).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRoleAsync_throws_when_renaming_to_an_existing_role()
    {
        var storage = new RoleStorage(_factory);
        await storage.CreateRoleAsync(new RoleUpsertRequest("User"));
        await storage.CreateRoleAsync(new RoleUpsertRequest("SuperAdmin"));

        var act = () => storage.UpdateRoleAsync("User", new RoleUpsertRequest("SuperAdmin"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*SuperAdmin*already exists*");
    }

    [Fact]
    public async Task UpdateRoleAsync_throws_when_the_role_does_not_exist()
    {
        var storage = new RoleStorage(_factory);

        var act = () => storage.UpdateRoleAsync("missing", new RoleUpsertRequest("Other"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteRoleAsync_removes_the_role_and_ignores_missing_ones()
    {
        var storage = new RoleStorage(_factory);
        await storage.CreateRoleAsync(new RoleUpsertRequest("User"));

        await storage.DeleteRoleAsync("User");
        await storage.DeleteRoleAsync("User");

        (await storage.GetRoleAsync("User")).Should().BeNull();
    }
}
