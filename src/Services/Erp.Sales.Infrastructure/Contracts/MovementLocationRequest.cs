namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="Address">Street address, mandatory for the transport regime.</param>
public sealed record MovementLocationRequest(
    string Address,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? WarehouseId = null,
    string? LocationId = null);
