namespace Erp.Common;

/// <summary>
/// Marks a table that wiping a company's data must leave alone: what makes the company a company
/// (its warehouses, its members, its subscription, its tax authority credentials) rather than what
/// it has done. Read by <c>CompanyDataPurger</c>, which deletes everything else that belongs to the
/// company.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SurvivesCompanyResetAttribute : Attribute;
