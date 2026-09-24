namespace Erp.Api.Security;

/// <summary>
/// Opts an action or controller out of <see cref="RequireActiveSubscriptionFilter"/> — for the
/// handful of endpoints that legitimately have to work for a company with no active subscription:
/// creating the company itself, self-service sign-up, and the subscription/plan management
/// endpoints a company (or a SuperAdmin on its behalf) uses to actually get one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowWithoutSubscriptionAttribute : Attribute;
