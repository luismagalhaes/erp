namespace Erp.Sales.Infrastructure.Contracts;

public sealed record MovementLocationRequest(
    string Address,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? WarehouseId = null,
    string? LocationId = null);

/// <param name="TaxId">Leave empty for an unidentified final consumer.</param>
public sealed record MovementPartyRequest(string? TaxId, string Name, bool IsSupplier = false);

public sealed record CreateStockMovementLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxPercentage,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null);

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

public sealed record VoidStockMovementRequest(string Reason);

/// <param name="AtDocCodeId">Code the tax authority returns when the document is communicated.</param>
public sealed record CommunicateStockMovementRequest(string AtDocCodeId);

public sealed record StockMovementListItemDto(
    Guid Id,
    string DocumentNumber,
    string MovementType,
    string Atcud,
    DateOnly MovementDate,
    string PartyName,
    string PartyTaxId,
    DateTime MovementStartAtUtc,
    decimal TotalQuantity,
    decimal GrossTotal,
    string Status,
    string? AtDocCodeId);

public sealed record StockMovementLineDto(
    int LineNumber,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal LineAmount,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxAmount);

public sealed record MovementLocationDto(
    string Address,
    string? City,
    string? PostalCode,
    string Country,
    string? WarehouseId,
    string? LocationId);

public sealed record StockMovementDetailDto(
    Guid Id,
    string DocumentNumber,
    string MovementType,
    string Atcud,
    DateOnly MovementDate,
    DateTime SystemEntryDateUtc,
    string Status,
    string PartyName,
    string PartyTaxId,
    bool PartyIsSupplier,
    MovementLocationDto ShipFrom,
    MovementLocationDto ShipTo,
    DateTime MovementStartAtUtc,
    DateTime? MovementEndAtUtc,
    string? VehiclePlate,
    string? Comments,
    decimal TotalQuantity,
    decimal NetTotal,
    decimal TaxPayable,
    decimal GrossTotal,
    string PrintableHash,
    string QrCodePayload,
    string? AtDocCodeId,
    DateTime? CommunicatedAtUtc,
    IReadOnlyList<StockMovementLineDto> Lines);
