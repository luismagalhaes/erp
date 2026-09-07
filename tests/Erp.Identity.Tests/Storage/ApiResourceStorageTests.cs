using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class ApiResourceStorageTests
{
    private readonly InMemoryConfigurationDbContextFactory _factory = new();

    private static ApiResourceUpsertRequest CreateRequest(
        string name = " erp-api ",
        IReadOnlyList<string>? scopes = null,
        IReadOnlyList<string>? userClaims = null) => new(
            name,
            "  ERP API  ",
            "   ",
            Enabled: true,
            ShowInDiscoveryDocument: true,
            RequireResourceIndicator: false,
            Scopes: scopes ?? ["erp.read"],
            UserClaims: userClaims ?? ["role"]);

    [Fact]
    public async Task CreateApiResourceAsync_trims_values_and_stores_the_collections()
    {
        var storage = new ApiResourceStorage(_factory);

        await storage.CreateApiResourceAsync(CreateRequest(scopes: [" erp.read ", "ERP.READ", "erp.write"]));

        var resource = await storage.GetApiResourceAsync("erp-api");

        resource.Should().NotBeNull();
        resource!.DisplayName.Should().Be("ERP API");
        resource.Description.Should().BeEmpty();
        resource.Scopes.Should().BeEquivalentTo(["erp.read", "erp.write"]);
        resource.UserClaims.Should().ContainSingle().Which.Should().Be("role");
    }

    [Fact]
    public async Task CreateApiResourceAsync_throws_when_the_resource_already_exists()
    {
        var storage = new ApiResourceStorage(_factory);
        await storage.CreateApiResourceAsync(CreateRequest());

        var act = () => storage.CreateApiResourceAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*erp-api*already exists*");
    }

    [Fact]
    public async Task GetApiResourcesAsync_returns_the_resources_ordered_by_name()
    {
        var storage = new ApiResourceStorage(_factory);
        await storage.CreateApiResourceAsync(CreateRequest("zeta-api"));
        await storage.CreateApiResourceAsync(CreateRequest("alpha-api"));

        var resources = await storage.GetApiResourcesAsync();

        resources.Select(x => x.Name).Should().ContainInOrder("alpha-api", "zeta-api");
    }

    [Fact]
    public async Task GetApiResourceAsync_returns_null_when_the_resource_does_not_exist()
    {
        var storage = new ApiResourceStorage(_factory);

        (await storage.GetApiResourceAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateApiResourceAsync_renames_the_resource_and_replaces_the_collections()
    {
        var storage = new ApiResourceStorage(_factory);
        await storage.CreateApiResourceAsync(CreateRequest());

        await storage.UpdateApiResourceAsync("erp-api", CreateRequest("erp-api-v2", ["erp.write"], ["name"]));

        (await storage.GetApiResourceAsync("erp-api")).Should().BeNull();
        var resource = await storage.GetApiResourceAsync("erp-api-v2");
        resource!.Scopes.Should().ContainSingle().Which.Should().Be("erp.write");
        resource.UserClaims.Should().ContainSingle().Which.Should().Be("name");
    }

    [Fact]
    public async Task UpdateApiResourceAsync_throws_when_renaming_to_an_existing_resource()
    {
        var storage = new ApiResourceStorage(_factory);
        await storage.CreateApiResourceAsync(CreateRequest("erp-api"));
        await storage.CreateApiResourceAsync(CreateRequest("other-api"));

        var act = () => storage.UpdateApiResourceAsync("erp-api", CreateRequest("other-api"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*other-api*already exists*");
    }

    [Fact]
    public async Task UpdateApiResourceAsync_throws_when_the_resource_does_not_exist()
    {
        var storage = new ApiResourceStorage(_factory);

        var act = () => storage.UpdateApiResourceAsync("missing", CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteApiResourceAsync_removes_the_resource_and_ignores_missing_ones()
    {
        var storage = new ApiResourceStorage(_factory);
        await storage.CreateApiResourceAsync(CreateRequest());

        await storage.DeleteApiResourceAsync("erp-api");
        await storage.DeleteApiResourceAsync("erp-api");

        (await storage.GetApiResourceAsync("erp-api")).Should().BeNull();
    }
}
