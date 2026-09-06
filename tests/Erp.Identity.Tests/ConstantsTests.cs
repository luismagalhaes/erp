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
            Constants.Roles.Admin,
            Constants.Roles.Manager,
            Constants.Roles.User,
            Constants.Roles.Accountant,
            Constants.Roles.Auditor
        ]);
    }

    [Fact]
    public void Every_api_scope_follows_the_erp_module_action_naming()
    {
        var scopes = new[]
        {
            Constants.Scopes.ErpCoreRead, Constants.Scopes.ErpCoreWrite,
            Constants.Scopes.ErpSalesRead, Constants.Scopes.ErpSalesWrite,
            Constants.Scopes.ErpInventoryRead, Constants.Scopes.ErpInventoryWrite,
            Constants.Scopes.ErpPurchasingRead, Constants.Scopes.ErpPurchasingWrite,
            Constants.Scopes.ErpAccountingRead, Constants.Scopes.ErpAccountingWrite,
            Constants.Scopes.ErpReportingRead,
            Constants.Scopes.ErpNotificationRead, Constants.Scopes.ErpNotificationWrite,
            Constants.Scopes.ErpNotificationSend,
            Constants.Scopes.ErpIdentityRead
        };

        scopes.Should().OnlyHaveUniqueItems();
        scopes.Should().AllSatisfy(scope =>
        {
            scope.Should().StartWith("erp.");
            scope.Split('.').Should().HaveCount(3);
            scope.Should().MatchRegex("^erp\\.[a-z]+\\.(read|write|send)$");
        });
    }

    [Fact]
    public void Api_resource_names_match_the_audiences_the_services_validate()
    {
        var resources = new[]
        {
            Constants.ApiResources.CoreApi,
            Constants.ApiResources.SalesApi,
            Constants.ApiResources.InventoryApi,
            Constants.ApiResources.PurchasingApi,
            Constants.ApiResources.AccountingApi,
            Constants.ApiResources.ReportingApi,
            Constants.ApiResources.NotificationApi,
            Constants.ApiResources.IdentityApi
        };

        resources.Should().OnlyHaveUniqueItems();
        resources.Should().AllSatisfy(resource => resource.Should().EndWith("-api"));
    }
}
