using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using FluentAssertions;

namespace Erp.Identity.Tests;

/// <summary>
/// The seed is replayed on every start, so a mistake here silently reconfigures the whole
/// authorization surface. These tests pin the invariants the rest of the system depends on.
/// </summary>
public class SeedDataTests
{
    [Fact]
    public void Every_api_resource_scope_is_a_declared_api_scope()
    {
        var declaredScopes = SeedData.ApiScopes.Select(scope => scope.Name).ToHashSet(StringComparer.Ordinal);

        var unknown = SeedData.ApiResources
            .SelectMany(resource => resource.Scopes)
            .Where(scope => !declaredScopes.Contains(scope))
            .ToList();

        unknown.Should().BeEmpty("an API resource cannot reference a scope that is never created");
    }

    [Fact]
    public void Every_client_scope_is_either_an_identity_resource_or_an_api_scope()
    {
        var known = SeedData.ApiScopes.Select(scope => scope.Name)
            .Concat(SeedData.IdentityResources.Select(resource => resource.Name))
            .Append(Constants.Scopes.OfflineAccess)
            .ToHashSet(StringComparer.Ordinal);

        var unknown = SeedData.Clients
            .SelectMany(client => client.AllowedScopes)
            .Where(scope => !known.Contains(scope))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        unknown.Should().BeEmpty();
    }

    /// <summary>
    /// Without this claim the access token carries no roles, and every
    /// [Authorize(Roles = ...)] endpoint answers 403 even to a SuperAdmin.
    /// </summary>
    [Fact]
    public void Every_api_resource_asks_for_the_role_claim()
    {
        SeedData.ApiResources.Should().AllSatisfy(resource =>
            resource.UserClaims.Should().Contain(Constants.Claims.Role, $"{resource.Name} authorizes by role"));
    }

    /// <summary>
    /// The business modules run in one host, so a single audience has to cover every module
    /// scope. A scope left out here would answer 401 on the merged API.
    /// </summary>
    [Fact]
    public void The_business_audience_covers_every_module_scope()
    {
        var businessScopes = SeedData.ApiScopes
            .Select(scope => scope.Name)
            .Except([Constants.Scopes.ErpIdentityRead], StringComparer.Ordinal);

        SeedData.ApiResources
            .Single(resource => resource.Name == Constants.ApiResources.ErpApi)
            .Scopes.Should().Contain(businessScopes);
    }

    [Fact]
    public void Client_ids_are_unique()
    {
        var clientIds = SeedData.Clients.Select(client => client.ClientId).ToList();

        clientIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Api_scope_names_are_unique()
    {
        SeedData.ApiScopes.Select(scope => scope.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_main_ui_client_uses_authorization_code_with_pkce_and_no_secret()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.BlazorWasmClientId);

        client.AllowedGrantTypes.Should().Contain("authorization_code");
        client.RequirePkce.Should().BeTrue();
        client.RequireClientSecret.Should().BeFalse();
        client.ClientSecrets.Should().BeEmpty("a browser client cannot keep a secret");
        client.AllowOfflineAccess.Should().BeTrue();
    }

    /// <summary>
    /// The UI reaches every module, with one deliberate exception: queueing email is a
    /// service only capability, checked separately below.
    /// </summary>
    [Fact]
    public void The_main_ui_client_can_reach_every_api_scope_except_the_service_only_ones()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.BlazorWasmClientId);

        var userFacingScopes = SeedData.ApiScopes
            .Select(scope => scope.Name)
            .Except([Constants.Scopes.ErpNotificationSend], StringComparer.Ordinal);

        client.AllowedScopes.Should().Contain(userFacingScopes);
    }

    [Fact]
    public void The_main_ui_client_callbacks_match_the_configured_origins()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.BlazorWasmClientId);

        client.RedirectUris.Should().Contain(
            Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LoginCallbackPath);
        client.PostLogoutRedirectUris.Should().Contain(
            Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LogoutCallbackPath);
        client.AllowedCorsOrigins.Should().Contain(Constants.Clients.HttpsLocalhost7019);
    }

    [Theory]
    [InlineData("sales-service")]
    [InlineData("reporting-service")]
    [InlineData("identity-service")]
    public void Machine_clients_use_client_credentials_with_a_secret_and_no_redirects(string clientId)
    {
        var client = SeedData.Clients.Single(x => x.ClientId == clientId);

        client.AllowedGrantTypes.Should().Contain("client_credentials");
        client.ClientSecrets.Should().NotBeEmpty();
        client.RedirectUris.Should().BeEmpty();
        client.AllowedScopes.Should().NotContain(Constants.Scopes.OpenId);
    }

    [Fact]
    public void Machine_client_secrets_are_hashed_not_stored_in_clear_text()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.SalesServiceClientId);

        client.ClientSecrets.Should().AllSatisfy(secret =>
            secret.Value.Should().NotBe(Constants.Clients.SalesServiceSecret));
    }

    [Fact]
    public void Only_the_identity_service_may_queue_emails()
    {
        var allowed = SeedData.Clients
            .Where(client => client.AllowedScopes.Contains(Constants.Scopes.ErpNotificationSend))
            .Select(client => client.ClientId)
            .ToList();

        allowed.Should().Equal(Constants.Clients.IdentityServiceClientId);
    }

    [Fact]
    public void The_user_facing_client_cannot_send_mail_through_the_erp()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.BlazorWasmClientId);

        client.AllowedScopes.Should().NotContain(Constants.Scopes.ErpNotificationSend);
        client.AllowedScopes.Should().Contain(Constants.Scopes.ErpNotificationRead);
    }

    [Fact]
    public void The_identity_service_client_is_limited_to_queueing_emails()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.IdentityServiceClientId);

        client.AllowedScopes.Should().Equal(Constants.Scopes.ErpNotificationSend);
    }

    [Fact]
    public void The_ui_client_can_read_users_to_assign_them_to_companies()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.BlazorWasmClientId);

        client.AllowedScopes.Should().Contain(Constants.Scopes.ErpIdentityRead);

        SeedData.ApiResources
            .Single(resource => resource.Name == Constants.ApiResources.IdentityApi)
            .Scopes.Should().Contain(Constants.Scopes.ErpIdentityRead);
    }

    [Fact]
    public void The_identity_resources_cover_the_scopes_the_ui_asks_for()
    {
        var names = SeedData.IdentityResources.Select(resource => resource.Name);

        names.Should().Contain([Constants.Scopes.OpenId, Constants.Scopes.Profile, Constants.Scopes.Email]);
    }
}
