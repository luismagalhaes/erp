namespace Erp.Api.Security;

/// <summary>
/// Names the entity type a controller's <c>{id:guid}</c> actions operate on, so
/// <see cref="TenantAwareNotFoundFilter"/> can tell a row that genuinely does not exist apart from
/// one that exists but belongs to another company — the one thing <c>RequireCompanyAccessFilter</c>
/// cannot do, since those actions never carry a companyId of their own to check.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScopedEntityAttribute(Type entityType) : Attribute
{
    public Type EntityType { get; } = entityType;
}
