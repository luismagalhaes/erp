namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="TaxId">Leave empty for an unidentified final consumer.</param>
public sealed record CustomerRequest(
    string? TaxId,
    string Name,
    string? Address,
    string Country = "PT",
    string? PostalCode = null,
    string? City = null);
