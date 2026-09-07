namespace Erp.Sales.Infrastructure.Contracts;

public sealed record CreateStockMovementRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly MovementDate,
    MovementPartyRequest Party,
    MovementLocationRequest ShipFrom,
    MovementLocationRequest ShipTo,
    DateTime MovementStartAtUtc,
    IReadOnlyList<CreateStockMovementLineRequest> Lines,
    DateTime? MovementEndAtUtc = null,
    string? VehiclePlate = null,
    string? Comments = null);
