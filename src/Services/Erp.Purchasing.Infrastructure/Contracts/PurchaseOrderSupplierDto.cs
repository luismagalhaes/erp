namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>The supplier as it stood when the order was written, not as it stands now.</summary>
public sealed record PurchaseOrderSupplierDto(
    string Code,
    string Name,
    string TaxId,
    string? Address = null,
    string? PostalCode = null,
    string? City = null,
    string Country = "PT");
