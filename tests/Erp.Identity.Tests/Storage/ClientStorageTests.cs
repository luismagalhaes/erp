using Duende.IdentityServer.EntityFramework.Entities;
using Duende.IdentityServer.Models;
using Erp.Identity.Domain.Application;
using Erp.Identity.Storage.Storage;
using FluentAssertions;

namespace Erp.Identity.Tests.Storage;

public class ClientStorageTests
{
    private readonly InMemoryConfigurationDbContextFactory _factory = new();

    private static ClientUpsertRequest CreateRequest(
        string clientId = "blazor-wasm",
        IReadOnlyList<ClientSecretItem>? secrets = null) => new(
            clientId,
            "Blazor WASM",
            Enabled: true,
            RequirePkce: true,
            RequireClientSecret: false,
            AllowOfflineAccess: true,
            AccessTokenLifetime: 3600,
            SlidingRefreshTokenLifetime: 1296000,
            AbsoluteRefreshTokenLifetime: 2592000,
            RefreshTokenUsage: "OneTimeOnly",
            RefreshTokenExpiration: "Absolute",
            AllowedGrantTypes: ["authorization_code"],
            RedirectUris: ["https://localhost/callback"],
            PostLogoutRedirectUris: ["https://localhost/"],
            AllowedCorsOrigins: ["https://localhost"],
            AllowedScopes: ["openid", "erp.read"],
            ClientSecrets: secrets ?? []);

    [Fact]
    public async Task CreateClientAsync_persists_the_client_with_all_collections()
    {
        var storage = new ClientStorage(_factory);

        await storage.CreateClientAsync(CreateRequest());

        var client = await storage.GetClientAsync("blazor-wasm");

        client.Should().NotBeNull();
        client!.ClientName.Should().Be("Blazor WASM");
        client.AllowedGrantTypes.Should().ContainSingle().Which.Should().Be("authorization_code");
        client.RedirectUris.Should().ContainSingle().Which.Should().Be("https://localhost/callback");
        client.PostLogoutRedirectUris.Should().ContainSingle();
        client.AllowedCorsOrigins.Should().ContainSingle();
        client.AllowedScopes.Should().BeEquivalentTo(["erp.read", "openid"]);
        client.RefreshTokenUsage.Should().Be("OneTimeOnly");
        client.RefreshTokenExpiration.Should().Be("Absolute");
    }

    [Fact]
    public async Task CreateClientAsync_hashes_plain_text_secrets()
    {
        var storage = new ClientStorage(_factory);
        var expected = "plain".Sha256();

        await storage.CreateClientAsync(CreateRequest(secrets: [new ClientSecretItem("plain", "  primary  ", IsHashed: false)]));

        var client = await storage.GetClientAsync("blazor-wasm");

        var secret = client!.ClientSecrets.Should().ContainSingle().Subject;
        secret.Value.Should().Be(expected);
        secret.Description.Should().Be("primary");
        secret.IsHashed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateClientAsync_keeps_already_hashed_secrets_untouched()
    {
        var storage = new ClientStorage(_factory);

        await storage.CreateClientAsync(CreateRequest(secrets: [new ClientSecretItem("already-hashed", null, IsHashed: true)]));

        var client = await storage.GetClientAsync("blazor-wasm");

        client!.ClientSecrets.Should().ContainSingle().Which.Value.Should().Be("already-hashed");
    }

    [Fact]
    public async Task CreateClientAsync_throws_when_the_client_already_exists()
    {
        var storage = new ClientStorage(_factory);
        await storage.CreateClientAsync(CreateRequest());

        var act = () => storage.CreateClientAsync(CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blazor-wasm*already exists*");
    }

    [Fact]
    public async Task GetClientAsync_returns_null_when_the_client_does_not_exist()
    {
        var storage = new ClientStorage(_factory);

        var client = await storage.GetClientAsync("missing");

        client.Should().BeNull();
    }

    [Fact]
    public async Task GetClientsAsync_returns_the_clients_ordered_by_id()
    {
        var storage = new ClientStorage(_factory);
        await storage.CreateClientAsync(CreateRequest("zeta"));
        await storage.CreateClientAsync(CreateRequest("alpha"));

        var clients = await storage.GetClientsAsync();

        clients.Select(x => x.ClientId).Should().ContainInOrder("alpha", "zeta");
        clients[0].RedirectUris.Should().ContainSingle();
        clients[0].AllowedScopes.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateClientAsync_replaces_the_collections_instead_of_appending()
    {
        var storage = new ClientStorage(_factory);
        await storage.CreateClientAsync(CreateRequest());

        var request = CreateRequest() with
        {
            ClientName = "Renamed",
            Enabled = false,
            RefreshTokenUsage = "ReUse",
            RefreshTokenExpiration = "Sliding",
            RedirectUris = ["https://localhost/new"],
            AllowedScopes = ["erp.write"],
            ClientSecrets = [new ClientSecretItem("s", null, IsHashed: true)]
        };

        await storage.UpdateClientAsync("blazor-wasm", request);

        var client = await storage.GetClientAsync("blazor-wasm");

        client!.ClientName.Should().Be("Renamed");
        client.Enabled.Should().BeFalse();
        client.RefreshTokenUsage.Should().Be("ReUse");
        client.RefreshTokenExpiration.Should().Be("Sliding");
        client.RedirectUris.Should().ContainSingle().Which.Should().Be("https://localhost/new");
        client.AllowedScopes.Should().ContainSingle().Which.Should().Be("erp.write");
        client.ClientSecrets.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateClientAsync_throws_when_the_client_does_not_exist()
    {
        var storage = new ClientStorage(_factory);

        var act = () => storage.UpdateClientAsync("missing", CreateRequest());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*was not found*");
    }

    [Fact]
    public async Task DeleteClientAsync_removes_the_client()
    {
        var storage = new ClientStorage(_factory);
        await storage.CreateClientAsync(CreateRequest());

        await storage.DeleteClientAsync("blazor-wasm");

        (await storage.GetClientAsync("blazor-wasm")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteClientAsync_is_idempotent_when_the_client_does_not_exist()
    {
        var storage = new ClientStorage(_factory);

        var act = () => storage.DeleteClientAsync("missing");

        await act.Should().NotThrowAsync();
    }
}
