namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// Identification of the taxable entity for the inventory file header. The company file belongs to
/// Core, so the caller supplies it.
/// </summary>
public sealed record InventoryCompanyInfo(string Name, string TaxId, string? LegalName = null);
