using System.Security.Claims;
using Erp.Common;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Services;

/// <summary>
/// Authorization of the whole ERP API. Every business module is served by this host, so access
/// is granted in two levels instead of one pair of scopes per module: what a caller may reach
/// inside the API is then decided by role and by company membership.
/// </summary>
public static class Policies
{
    /// <summary>Reading data. Satisfied by a write scope as well.</summary>
    public const string Read = Constants.Scopes.ErpRead;

    /// <summary>Changing data.</summary>
    public const string Write = Constants.Scopes.ErpWrite;

    /// <summary>Configuration that affects the whole tenant: companies, memberships, series. Role only.</summary>
    public const string Admin = Constants.Roles.SuperAdmin;

    /// <summary>Queueing email. Service only, never granted to the user facing client.</summary>
    public const string NotificationSend = Constants.Scopes.ErpNotificationSend;

    public static AuthorizationBuilder AddPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(Read, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpRead, Constants.Scopes.ErpWrite)))
            .AddPolicy(Write, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpWrite)))
            // Administration is decided by role alone: being a SuperAdmin is what grants access
            // to tenant wide configuration.
            .AddPolicy(Admin, policy => policy.RequireAssertion(context =>
                context.User.IsInRole(Constants.Roles.SuperAdmin)))
            .AddPolicy(NotificationSend, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpNotificationSend)));

    /// <summary>
    /// Identity servers emit scopes either as one space separated claim or as repeated claims,
    /// so both shapes are accepted.
    /// </summary>
    private static bool HasScope(ClaimsPrincipal user, params string[] acceptedScopes)
    {
        var granted = user.FindAll(Constants.Claims.Scope)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return granted.Any(scope => acceptedScopes.Contains(scope, StringComparer.Ordinal));
    }
}
