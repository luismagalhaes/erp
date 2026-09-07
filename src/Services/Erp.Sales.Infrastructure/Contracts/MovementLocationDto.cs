namespace Erp.Sales.Infrastructure.Contracts;

public sealed record MovementLocationDto(
    string Address,
    string? City,
    string? PostalCode,
    string Country,
    string? WarehouseId,
    string? LocationId);
