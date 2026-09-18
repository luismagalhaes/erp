namespace Erp.Api.Security;

/// <summary>
/// Opts an action or controller out of <see cref="RequireCompanyAccessFilter"/> — for the handful
/// of endpoints that legitimately reach across every company: the tenant list itself, or a caller
/// checking its own access, where a "no access" answer has to come back as data, not a 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowAnyCompanyAttribute : Attribute;
