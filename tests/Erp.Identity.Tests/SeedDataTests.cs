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
    /// The business modules run in one host, so a single audience has to cover every scope of
    /// the API. A scope left out here would answer 401 on the merged API.
    /// </summary>
    [Fact]
    public void The_business_audience_covers_every_module_scope()
    {
        var businessScopes = SeedData.ApiScopes
            .Select(scope => scope.Name)
            .Except([Constants.Scopes.ErpIdentityRead, Constants.Scopes.ErpIdentityOnboarding], StringComparer.Ordinal);

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
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpPortalClientId);

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
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpPortalClientId);

        var userFacingScopes = SeedData.ApiScopes
            .Select(scope => scope.Name)
            .Except([Constants.Scopes.ErpNotificationSend, Constants.Scopes.ErpIdentityOnboarding], StringComparer.Ordinal);

        client.AllowedScopes.Should().Contain(userFacingScopes);
    }

    /// <summary>
    /// The ERP API is the authority on who may invite whom, so only it may raise an onboarding
    /// request: a user token, which any signed-in person holds, must never be enough.
    /// </summary>
    [Fact]
    public void Only_the_erp_api_client_may_raise_onboarding_requests()
    {
        var allowed = SeedData.Clients
            .Where(client => client.AllowedScopes.Contains(Constants.Scopes.ErpIdentityOnboarding))
            .Select(client => client.ClientId)
            .ToList();

        allowed.Should().Equal(Constants.Clients.ErpApiClientId);
    }

    [Fact]
    public void The_identity_audience_carries_the_onboarding_scope()
    {
        SeedData.ApiResources
            .Single(resource => resource.Name == Constants.ApiResources.IdentityApi)
            .Scopes.Should().Contain(Constants.Scopes.ErpIdentityOnboarding);
    }

    [Fact]
    public void The_main_ui_client_callbacks_match_the_configured_origins()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpPortalClientId);

        client.RedirectUris.Should().Contain(
            Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LoginCallbackPath);
        client.PostLogoutRedirectUris.Should().Contain(
            Constants.Clients.HttpsLocalhost7019 + Constants.Clients.LogoutCallbackPath);
        client.AllowedCorsOrigins.Should().Contain(Constants.Clients.HttpsLocalhost7019);
    }

    /// <summary>
    /// The seed deliberately does not create a secret for this client — it only *creates*
    /// records, it never updates one, so a secret baked in here would be the same in every
    /// environment forever. It must be set from the backoffice after first deploy.
    /// </summary>
    [Theory]
    [InlineData("erp-api")]
    [InlineData("erp-identity")]
    public void Machine_clients_use_client_credentials_and_start_with_no_seeded_secret(string clientId)
    {
        var client = SeedData.Clients.Single(x => x.ClientId == clientId);

        client.AllowedGrantTypes.Should().Contain("client_credentials");
        client.RequireClientSecret.Should().BeTrue();
        client.ClientSecrets.Should().BeEmpty("the secret is set from the backoffice, not seeded");
        client.RedirectUris.Should().BeEmpty();
        client.AllowedScopes.Should().NotContain(Constants.Scopes.OpenId);
    }

    [Fact]
    public void Only_the_erp_identity_client_may_queue_emails()
    {
        var allowed = SeedData.Clients
            .Where(client => client.AllowedScopes.Contains(Constants.Scopes.ErpNotificationSend))
            .Select(client => client.ClientId)
            .ToList();

        allowed.Should().Equal(Constants.Clients.ErpIdentityClientId);
    }

    [Fact]
    public void The_user_facing_client_cannot_send_mail_through_the_erp()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpPortalClientId);

        client.AllowedScopes.Should().NotContain(Constants.Scopes.ErpNotificationSend);
        client.AllowedScopes.Should().Contain(Constants.Scopes.ErpRead);
    }

    [Fact]
    public void The_service_clients_are_limited_to_the_one_scope_of_their_direction()
    {
        SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpIdentityClientId)
            .AllowedScopes.Should().Equal(Constants.Scopes.ErpNotificationSend);

        SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpApiClientId)
            .AllowedScopes.Should().Equal(Constants.Scopes.ErpIdentityOnboarding);
    }

    [Fact]
    public void There_are_exactly_three_clients_the_portal_and_one_service_client_per_direction()
    {
        SeedData.Clients.Select(client => client.ClientId).Should().BeEquivalentTo(
            [Constants.Clients.ErpPortalClientId, Constants.Clients.ErpApiClientId, Constants.Clients.ErpIdentityClientId]);
    }

    [Fact]
    public void The_ui_client_can_read_users_to_assign_them_to_companies()
    {
        var client = SeedData.Clients.Single(x => x.ClientId == Constants.Clients.ErpPortalClientId);

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
