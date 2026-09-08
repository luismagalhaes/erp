namespace Erp.Sales.Domain;

/// <summary>
/// Loading or delivery point of a goods movement, exported as ShipFrom and ShipTo in the SAF-T
/// MovementOfGoods. Copied into the document at issuing time, like every other snapshot.
/// </summary>
/// <param name="Address">Street address, mandatory for the transport regime.</param>
/// <param name="WarehouseId">Optional warehouse identification.</param>
public sealed record MovementLocation(
    string Address,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? WarehouseId = null,
    string? LocationId = null);
