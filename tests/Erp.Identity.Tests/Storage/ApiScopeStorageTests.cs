using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class ApiScopeStorageTests
{
    private readonly InMemoryConfigurationDbContextFactory _factory = new();

    private static ApiScopeUpsertRequest CreateRequest(
        string name = " erp.read ",
        IReadOnlyList<string>? userClaims = null) => new(
            name,
            "  ERP Read  ",
            "   ",
            Enabled: true,
            Required: false,
            Emphasize: false,
            ShowInDiscoveryDocument: true,
            UserClaims: userClaims ?? []);

    [Fact]
    public async Task CreateApiScopeAsync_trims_the_name_and_nulls_blank_text()
    {
        var storage = new ApiScopeStorage(_factory);

        await storage.CreateApiScopeAsync(CreateRequest());

        var scope = await storage.GetApiScopeAsync("erp.read");

        scope.Should().NotBeNull();
        scope!.DisplayName.Should().Be("ERP Read");
        scope.Description.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateApiScopeAsync_deduplicates_and_trims_the_user_claims()
    {
        var storage = new ApiScopeStorage(_factory);

        await storage.CreateApiScopeAsync(CreateRequest(userClaims: [" role ", "ROLE", "  ", "name"]));

        var scope = await storage.GetApiScopeAsync("erp.read");

        scope!.UserClaims.Should().BeEquivalentTo(["name", "role"]);
    }

    [Fact]
    public async Task CreateApiScopeAsync_throws_when_the_scope_already_exists()
    {
        var storage = new ApiScopeStorage(_factory);
        await storage.CreateApiScopeAsync(CreateRequest());

        var act = () => storage.CreateApiScopeAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*erp.read*already exists*");
    }

    [Fact]
    public async Task GetApiScopesAsync_returns_the_scopes_ordered_by_name()
    {
        var storage = new ApiScopeStorage(_factory);
        await storage.CreateApiScopeAsync(CreateRequest("erp.write"));
        await storage.CreateApiScopeAsync(CreateRequest("erp.read", ["role"]));

        var scopes = await storage.GetApiScopesAsync();

        scopes.Select(x => x.Name).Should().ContainInOrder("erp.read", "erp.write");
        scopes[0].UserClaims.Should().ContainSingle().Which.Should().Be("role");
    }

    [Fact]
    public async Task GetApiScopeAsync_returns_null_when_the_scope_does_not_exist()
    {
        var storage = new ApiScopeStorage(_factory);

        (await storage.GetApiScopeAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateApiScopeAsync_renames_the_scope_and_replaces_the_claims()
    {
        var storage = new ApiScopeStorage(_factory);
        await storage.CreateApiScopeAsync(CreateRequest("erp.read", ["role"]));

        await storage.UpdateApiScopeAsync("erp.read", CreateRequest("erp.write", ["name"]));

        (await storage.GetApiScopeAsync("erp.read")).Should().BeNull();
        var scope = await storage.GetApiScopeAsync("erp.write");
        scope!.UserClaims.Should().ContainSingle().Which.Should().Be("name");
    }

    [Fact]
    public async Task UpdateApiScopeAsync_throws_when_renaming_to_an_existing_scope()
    {
        var storage = new ApiScopeStorage(_factory);
        await storage.CreateApiScopeAsync(CreateRequest("erp.read"));
        await storage.CreateApiScopeAsync(CreateRequest("erp.write"));

        var act = () => storage.UpdateApiScopeAsync("erp.read", CreateRequest("erp.write"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*erp.write*already exists*");
    }

    [Fact]
    public async Task UpdateApiScopeAsync_throws_when_the_scope_does_not_exist()
    {
        var storage = new ApiScopeStorage(_factory);

        var act = () => storage.UpdateApiScopeAsync("missing", CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteApiScopeAsync_removes_the_scope_and_ignores_missing_ones()
    {
        var storage = new ApiScopeStorage(_factory);
        await storage.CreateApiScopeAsync(CreateRequest());

        await storage.DeleteApiScopeAsync("erp.read");
        await storage.DeleteApiScopeAsync("erp.read");

        (await storage.GetApiScopeAsync("erp.read")).Should().BeNull();
    }
}
