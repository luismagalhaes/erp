namespace Erp.Common;

/// <summary>
/// Marks an entity that <c>AppDbContext</c>'s per-company global query filter must leave alone,
/// even though it carries a <c>CompanyId</c> column. So far that is only the membership table
/// itself (<c>UserCompany</c>): the filter decides what a caller may see by first reading which
/// companies they belong to, so that one read can never be filtered by the very answer it is
/// computing — filtering it would make every caller's own membership rows invisible to the query
/// that is supposed to find them.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SkipTenantFilterAttribute : Attribute;
