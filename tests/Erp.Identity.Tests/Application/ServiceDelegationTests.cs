using Erp.Identity.Application.Handlers;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Identity.Tests.Application;

/// <summary>
/// The application services are thin wrappers over the storage layer. These tests
/// pin the delegation contract: every call must reach the matching storage member
/// with the arguments untouched, and the storage result must be returned as is.
/// </summary>
public class ServiceDelegationTests
{
    private static readonly CancellationToken Token = new CancellationTokenSource().Token;

    // ---------- ClientService ----------

    [Fact]
    public async Task ClientService_delegates_every_member_to_the_storage()
    {
        var storage = Substitute.For<IClientStorage>();
        var service = new ClientService(storage);

        var list = new List<ClientListItem>();
        var edit = new ClientEditItem("id", "name", true, true, true, true, 1, 2, 3, "OneTimeOnly", "Absolute", [], [], [], [], [], []);
        var request = ClientRequest();

        storage.GetClientsAsync(Token).Returns(list);
        storage.GetClientAsync("id", Token).Returns(edit);

        (await service.GetClientsAsync(Token)).Should().BeSameAs(list);
        (await service.GetClientAsync("id", Token)).Should().BeSameAs(edit);

        await service.CreateClientAsync(request, Token);
        await service.UpdateClientAsync("id", request, Token);
        await service.DeleteClientAsync("id", Token);

        await storage.Received(1).CreateClientAsync(request, Token);
        await storage.Received(1).UpdateClientAsync("id", request, Token);
        await storage.Received(1).DeleteClientAsync("id", Token);
    }

    [Fact]
    public async Task ClientService_returns_null_when_the_storage_has_no_client()
    {
        var storage = Substitute.For<IClientStorage>();
        storage.GetClientAsync("missing", Token).Returns((ClientEditItem?)null);

        (await new ClientService(storage).GetClientAsync("missing", Token)).Should().BeNull();
    }

    // ---------- RoleService ----------

    [Fact]
    public async Task RoleService_delegates_every_member_to_the_storage()
    {
        var storage = Substitute.For<IRoleStorage>();
        var service = new RoleService(storage);

        var list = new List<RoleListItem>();
        var edit = new RoleEditItem("SuperAdmin");
        var request = new RoleUpsertRequest("SuperAdmin");

        storage.GetRolesAsync(Token).Returns(list);
        storage.GetRoleAsync("SuperAdmin", Token).Returns(edit);

        (await service.GetRolesAsync(Token)).Should().BeSameAs(list);
        (await service.GetRoleAsync("SuperAdmin", Token)).Should().BeSameAs(edit);

        await service.CreateRoleAsync(request, Token);
        await service.UpdateRoleAsync("SuperAdmin", request, Token);
        await service.DeleteRoleAsync("SuperAdmin", Token);

        await storage.Received(1).CreateRoleAsync(request, Token);
        await storage.Received(1).UpdateRoleAsync("SuperAdmin", request, Token);
        await storage.Received(1).DeleteRoleAsync("SuperAdmin", Token);
    }

    [Fact]
    public async Task RoleService_returns_null_when_the_storage_has_no_role()
    {
        var storage = Substitute.For<IRoleStorage>();
        storage.GetRoleAsync("missing", Token).Returns((RoleEditItem?)null);

        (await new RoleService(storage).GetRoleAsync("missing", Token)).Should().BeNull();
    }

    // ---------- ApiResourceService ----------

    [Fact]
    public async Task ApiResourceService_delegates_every_member_to_the_storage()
    {
        var storage = Substitute.For<IApiResourceStorage>();
        var service = new ApiResourceService(storage);

        var list = new List<ApiResourceListItem>();
        var edit = new ApiResourceEditItem("erp-api", "ERP", "", true, true, false, [], []);
        var request = new ApiResourceUpsertRequest("erp-api", "ERP", "", true, true, false, [], []);

        storage.GetApiResourcesAsync(Token).Returns(list);
        storage.GetApiResourceAsync("erp-api", Token).Returns(edit);

        (await service.GetApiResourcesAsync(Token)).Should().BeSameAs(list);
        (await service.GetApiResourceAsync("erp-api", Token)).Should().BeSameAs(edit);

        await service.CreateApiResourceAsync(request, Token);
        await service.UpdateApiResourceAsync("erp-api", request, Token);
        await service.DeleteApiResourceAsync("erp-api", Token);

        await storage.Received(1).CreateApiResourceAsync(request, Token);
        await storage.Received(1).UpdateApiResourceAsync("erp-api", request, Token);
        await storage.Received(1).DeleteApiResourceAsync("erp-api", Token);
    }

    [Fact]
    public async Task ApiResourceService_returns_null_when_the_storage_has_no_resource()
    {
        var storage = Substitute.For<IApiResourceStorage>();
        storage.GetApiResourceAsync("missing", Token).Returns((ApiResourceEditItem?)null);

        (await new ApiResourceService(storage).GetApiResourceAsync("missing", Token)).Should().BeNull();
    }

    // ---------- IdentityResourceService ----------

    [Fact]
    public async Task IdentityResourceService_delegates_every_member_to_the_storage()
    {
        var storage = Substitute.For<IIdentityResourceStorage>();
        var service = new IdentityResourceService(storage);

        var list = new List<IdentityResourceListItem>();
        var edit = new IdentityResourceEditItem("profile", "Profile", "", true, false, false, true, []);
        var request = new IdentityResourceUpsertRequest("profile", "Profile", "", true, false, false, true, []);

        storage.GetIdentityResourcesAsync(Token).Returns(list);
        storage.GetIdentityResourceAsync("profile", Token).Returns(edit);

        (await service.GetIdentityResourcesAsync(Token)).Should().BeSameAs(list);
        (await service.GetIdentityResourceAsync("profile", Token)).Should().BeSameAs(edit);

        await service.CreateIdentityResourceAsync(request, Token);
        await service.UpdateIdentityResourceAsync("profile", request, Token);
        await service.DeleteIdentityResourceAsync("profile", Token);

        await storage.Received(1).CreateIdentityResourceAsync(request, Token);
        await storage.Received(1).UpdateIdentityResourceAsync("profile", request, Token);
        await storage.Received(1).DeleteIdentityResourceAsync("profile", Token);
    }

    [Fact]
    public async Task IdentityResourceService_returns_null_when_the_storage_has_no_resource()
    {
        var storage = Substitute.For<IIdentityResourceStorage>();
        storage.GetIdentityResourceAsync("missing", Token).Returns((IdentityResourceEditItem?)null);

        (await new IdentityResourceService(storage).GetIdentityResourceAsync("missing", Token)).Should().BeNull();
    }

    // ---------- IdentityProviderService ----------

    [Fact]
    public async Task IdentityProviderService_delegates_every_member_to_the_storage()
    {
        var storage = Substitute.For<IIdentityProviderStorage>();
        var service = new IdentityProviderService(storage);

        var list = new List<IdentityProviderListItem>();
        var edit = new IdentityProviderEditItem("google", "Google", "oidc", true);
        var request = new IdentityProviderUpsertRequest("google", "Google", "oidc", true);

        storage.GetIdentityProvidersAsync(Token).Returns(list);
        storage.GetIdentityProviderAsync("google", Token).Returns(edit);

        (await service.GetIdentityProvidersAsync(Token)).Should().BeSameAs(list);
        (await service.GetIdentityProviderAsync("google", Token)).Should().BeSameAs(edit);

        await service.CreateIdentityProviderAsync(request, Token);
        await service.UpdateIdentityProviderAsync("google", request, Token);
        await service.DeleteIdentityProviderAsync("google", Token);

        await storage.Received(1).CreateIdentityProviderAsync(request, Token);
        await storage.Received(1).UpdateIdentityProviderAsync("google", request, Token);
        await storage.Received(1).DeleteIdentityProviderAsync("google", Token);
    }

    [Fact]
    public async Task IdentityProviderService_returns_null_when_the_storage_has_no_provider()
    {
        var storage = Substitute.For<IIdentityProviderStorage>();
        storage.GetIdentityProviderAsync("missing", Token).Returns((IdentityProviderEditItem?)null);

        (await new IdentityProviderService(storage).GetIdentityProviderAsync("missing", Token)).Should().BeNull();
    }

    // ---------- ApiScopeService ----------

    [Fact]
    public async Task ApiScopeService_delegates_the_remaining_members_to_the_storage()
    {
        var storage = Substitute.For<IApiScopeStorage>();
        var service = new ApiScopeService(storage);

        var list = new List<ApiScopeListItem>();
        var request = new ApiScopeUpsertRequest("erp.read", "Read", "", true, false, false, true, []);

        storage.GetApiScopesAsync(Token).Returns(list);

        (await service.GetApiScopesAsync(Token)).Should().BeSameAs(list);

        await service.UpdateApiScopeAsync("erp.read", request, Token);
        await service.DeleteApiScopeAsync("erp.read", Token);

        await storage.Received(1).UpdateApiScopeAsync("erp.read", request, Token);
        await storage.Received(1).DeleteApiScopeAsync("erp.read", Token);
    }

    // ---------- UserService ----------

    [Fact]
    public async Task UserService_delegates_the_user_lookup_to_the_storage()
    {
        var storage = Substitute.For<IUserStorage>();
        var edit = new UserEditItem("id", "ana@erp.local", "Ana", true, [], "pt-PT");
        storage.GetUserAsync("id", Token).Returns(edit);

        (await new UserService(storage).GetUserAsync("id", Token)).Should().BeSameAs(edit);
    }

    private static ClientUpsertRequest ClientRequest() => new(
        "id", "name", true, true, true, true, 1, 2, 3, "OneTimeOnly", "Absolute", [], [], [], [], [], []);
}
