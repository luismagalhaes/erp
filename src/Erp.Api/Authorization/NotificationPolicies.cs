using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Erp.Api.Authorization;

public static class NotificationPolicies
{
    public const string Read = "notification.read";
    public const string Write = "notification.write";
    public const string Send = "notification.send";

    private const string ReadScope = "erp.notification.read";
    private const string WriteScope = "erp.notification.write";
    private const string SendScope = "erp.notification.send";
    private const string ScopeClaimType = "scope";

    public static AuthorizationBuilder AddNotificationPolicies(this AuthorizationBuilder builder) =>
        builder
            .AddPolicy(Read, policy => policy.RequireAssertion(context => HasScope(context.User, ReadScope, WriteScope)))
            .AddPolicy(Write, policy => policy.RequireAssertion(context => HasScope(context.User, WriteScope)))
            // Queueing is granted only to services, never to the user facing client, so that a
            // signed in user cannot send arbitrary mail through the ERP.
            .AddPolicy(Send, policy => policy.RequireAssertion(context => HasScope(context.User, SendScope)));

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
