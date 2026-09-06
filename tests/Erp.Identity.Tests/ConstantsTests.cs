using Erp.Identity.Common.Constants;
using FluentAssertions;

namespace Erp.Identity.Tests;

public class ConstantsTests
{
    [Fact]
    public void Roles_All_lists_every_declared_role_once()
    {
        Constants.Roles.All.Should().OnlyHaveUniqueItems();
        Constants.Roles.All.Should().Contain([
            Constants.Roles.SuperAdmin,
            Constants.Roles.User
        ]);
    }

    /// <summary>
    /// One API means two levels of access; only the capabilities that a signed in user must
    /// never hold keep a scope of their own.
    /// </summary>
    [Fact]
    public void Every_api_scope_follows_the_erp_naming()
    {
        var scopes = new[]
        {
            Constants.Scopes.ErpRead,
            Constants.Scopes.ErpWrite,
            Constants.Scopes.ErpNotificationSend,
            Constants.Scopes.ErpIdentityRead
        };

        scopes.Should().OnlyHaveUniqueItems();
        scopes.Should().AllSatisfy(scope =>
        {
            scope.Should().StartWith("erp.");
            scope.Should().MatchRegex("^erp\\.([a-z]+\\.)?(read|write|send)$");
        });
    }

    /// <summary>
    /// There are two hosts, so there are two audiences: the business modules share one, and the
    /// Identity host serves its own users API. Access between modules is separated by scope.
    /// </summary>
    [Fact]
    public void Api_resource_names_match_the_audiences_the_hosts_validate()
    {
        var resources = new[]
        {
            Constants.ApiResources.ErpApi,
            Constants.ApiResources.IdentityApi
        };

        resources.Should().OnlyHaveUniqueItems();
        resources.Should().AllSatisfy(resource => resource.Should().EndWith("-api"));
        Constants.ApiResources.ErpApi.Should().Be("erp-api");
    }
}
