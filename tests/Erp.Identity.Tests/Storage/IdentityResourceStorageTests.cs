using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class IdentityResourceStorageTests
{
    private readonly InMemoryConfigurationDbContextFactory _factory = new();

    private static IdentityResourceUpsertRequest CreateRequest(
        string name = " profile ",
        IReadOnlyList<string>? userClaims = null) => new(
            name,
            "  Profile  ",
            "   ",
            Enabled: true,
            Required: false,
            Emphasize: false,
            ShowInDiscoveryDocument: true,
            UserClaims: userClaims ?? ["name"]);

    [Fact]
    public async Task CreateIdentityResourceAsync_trims_values_and_stores_the_claims()
    {
        var storage = new IdentityResourceStorage(_factory);

        await storage.CreateIdentityResourceAsync(CreateRequest(userClaims: [" name ", "NAME", "  ", "family_name"]));

        var resource = await storage.GetIdentityResourceAsync("profile");

        resource.Should().NotBeNull();
        resource!.DisplayName.Should().Be("Profile");
        resource.Description.Should().BeEmpty();
        resource.UserClaims.Should().BeEquivalentTo(["family_name", "name"]);
    }

    [Fact]
    public async Task CreateIdentityResourceAsync_throws_when_the_resource_already_exists()
    {
        var storage = new IdentityResourceStorage(_factory);
        await storage.CreateIdentityResourceAsync(CreateRequest());

        var act = () => storage.CreateIdentityResourceAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*profile*already exists*");
    }

    [Fact]
    public async Task GetIdentityResourcesAsync_returns_the_resources_ordered_by_name()
    {
        var storage = new IdentityResourceStorage(_factory);
        await storage.CreateIdentityResourceAsync(CreateRequest("profile"));
        await storage.CreateIdentityResourceAsync(CreateRequest("email"));

        var resources = await storage.GetIdentityResourcesAsync();

        resources.Select(x => x.Name).Should().ContainInOrder("email", "profile");
        resources[0].UserClaims.Should().ContainSingle().Which.Should().Be("name");
    }

    [Fact]
    public async Task GetIdentityResourceAsync_returns_null_when_the_resource_does_not_exist()
    {
        var storage = new IdentityResourceStorage(_factory);

        (await storage.GetIdentityResourceAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateIdentityResourceAsync_renames_the_resource_and_replaces_the_claims()
    {
        var storage = new IdentityResourceStorage(_factory);
        await storage.CreateIdentityResourceAsync(CreateRequest());

        await storage.UpdateIdentityResourceAsync("profile", CreateRequest("email", ["email"]));

        (await storage.GetIdentityResourceAsync("profile")).Should().BeNull();
        var resource = await storage.GetIdentityResourceAsync("email");
        resource!.UserClaims.Should().ContainSingle().Which.Should().Be("email");
    }

    [Fact]
    public async Task UpdateIdentityResourceAsync_throws_when_renaming_to_an_existing_resource()
    {
        var storage = new IdentityResourceStorage(_factory);
        await storage.CreateIdentityResourceAsync(CreateRequest("profile"));
        await storage.CreateIdentityResourceAsync(CreateRequest("email"));

        var act = () => storage.UpdateIdentityResourceAsync("profile", CreateRequest("email"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*email*already exists*");
    }

    [Fact]
    public async Task UpdateIdentityResourceAsync_throws_when_the_resource_does_not_exist()
    {
        var storage = new IdentityResourceStorage(_factory);

        var act = () => storage.UpdateIdentityResourceAsync("missing", CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteIdentityResourceAsync_removes_the_resource_and_ignores_missing_ones()
    {
        var storage = new IdentityResourceStorage(_factory);
        await storage.CreateIdentityResourceAsync(CreateRequest());

        await storage.DeleteIdentityResourceAsync("profile");
        await storage.DeleteIdentityResourceAsync("profile");

        (await storage.GetIdentityResourceAsync("profile")).Should().BeNull();
    }
}
