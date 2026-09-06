using System.Security.Claims;
using Erp.Common;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Authorization;

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

    /// <summary>Configuration that affects the whole tenant: companies, memberships, series.</summary>
    public const string Admin = Constants.Roles.SuperAdmin;

    /// <summary>Queueing email. Service only, never granted to the user facing client.</summary>
    public const string NotificationSend = Constants.Scopes.ErpNotificationSend;

    private const string ScopeClaimType = "scope";

    public static AuthorizationBuilder AddErpPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(Read, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpRead, Constants.Scopes.ErpWrite)))
            .AddPolicy(Write, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpWrite)))
            // Administration is a write operation that only a SuperAdmin may perform, so the
            // scope alone is not enough: a service token cannot reconfigure the tenant.
            .AddPolicy(Admin, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpWrite) && context.User.IsInRole(Constants.Roles.SuperAdmin)))
            .AddPolicy(NotificationSend, policy => policy.RequireAssertion(context =>
                HasScope(context.User, Constants.Scopes.ErpNotificationSend)));

    /// <summary>
    /// Identity servers emit scopes either as one space separated claim or as repeated claims,
    /// so both shapes are accepted.
    /// </summary>
    private static bool HasScope(ClaimsPrincipal user, params string[] acceptedScopes)
    {
        var granted = user.FindAll(ScopeClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return granted.Any(scope => acceptedScopes.Contains(scope, StringComparer.Ordinal));
    }
}
