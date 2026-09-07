using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class IdentityProviderStorageTests
{
    private readonly InMemoryConfigurationDbContextFactory _factory = new();

    private static IdentityProviderUpsertRequest CreateRequest(string scheme = " google ")
        => new(scheme, "  Google  ", "  oidc  ", Enabled: true);

    [Fact]
    public async Task CreateIdentityProviderAsync_trims_the_values()
    {
        var storage = new IdentityProviderStorage(_factory);

        await storage.CreateIdentityProviderAsync(CreateRequest());

        var provider = await storage.GetIdentityProviderAsync("google");

        provider.Should().NotBeNull();
        provider!.DisplayName.Should().Be("Google");
        provider.Type.Should().Be("oidc");
        provider.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task CreateIdentityProviderAsync_throws_when_the_scheme_already_exists()
    {
        var storage = new IdentityProviderStorage(_factory);
        await storage.CreateIdentityProviderAsync(CreateRequest());

        var act = () => storage.CreateIdentityProviderAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*google*already exists*");
    }

    [Fact]
    public async Task GetIdentityProvidersAsync_returns_the_providers_ordered_by_scheme()
    {
        var storage = new IdentityProviderStorage(_factory);
        await storage.CreateIdentityProviderAsync(CreateRequest("google"));
        await storage.CreateIdentityProviderAsync(CreateRequest("azuread"));

        var providers = await storage.GetIdentityProvidersAsync();

        providers.Select(x => x.Scheme).Should().ContainInOrder("azuread", "google");
    }

    [Fact]
    public async Task GetIdentityProviderAsync_returns_null_when_the_provider_does_not_exist()
    {
        var storage = new IdentityProviderStorage(_factory);

        (await storage.GetIdentityProviderAsync("missing")).Should().BeNull();
    }

    [Fact]
    public async Task UpdateIdentityProviderAsync_renames_the_scheme()
    {
        var storage = new IdentityProviderStorage(_factory);
        await storage.CreateIdentityProviderAsync(CreateRequest("google"));

        await storage.UpdateIdentityProviderAsync("google", new IdentityProviderUpsertRequest("azuread", "Azure AD", "oidc", Enabled: false));

        (await storage.GetIdentityProviderAsync("google")).Should().BeNull();
        var provider = await storage.GetIdentityProviderAsync("azuread");
        provider!.DisplayName.Should().Be("Azure AD");
        provider.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateIdentityProviderAsync_throws_when_renaming_to_an_existing_scheme()
    {
        var storage = new IdentityProviderStorage(_factory);
        await storage.CreateIdentityProviderAsync(CreateRequest("google"));
        await storage.CreateIdentityProviderAsync(CreateRequest("azuread"));

        var act = () => storage.UpdateIdentityProviderAsync("google", CreateRequest("azuread"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*azuread*already exists*");
    }

    [Fact]
    public async Task UpdateIdentityProviderAsync_throws_when_the_provider_does_not_exist()
    {
        var storage = new IdentityProviderStorage(_factory);

        var act = () => storage.UpdateIdentityProviderAsync("missing", CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteIdentityProviderAsync_removes_the_provider_and_ignores_missing_ones()
    {
        var storage = new IdentityProviderStorage(_factory);
        await storage.CreateIdentityProviderAsync(CreateRequest());

        await storage.DeleteIdentityProviderAsync("google");
        await storage.DeleteIdentityProviderAsync("google");

        (await storage.GetIdentityProviderAsync("google")).Should().BeNull();
    }
}
