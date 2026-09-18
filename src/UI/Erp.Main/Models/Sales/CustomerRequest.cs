namespace Erp.Main.Models.Sales;

public sealed record CustomerRequest(
    string? TaxId,
    string Name,
    string? Address,
    string Country = "PT",
    string? PostalCode = null,
    string? City = null);
