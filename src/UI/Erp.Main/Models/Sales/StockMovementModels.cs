namespace Erp.Main.Models.Sales;

public sealed record MovementLocationRequest(
    string Address,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? WarehouseId = null,
    string? LocationId = null);

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

public sealed record CommunicateStockMovementRequest(string AtDocCodeId);

public sealed record StockMovementListItem(
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

public sealed record StockMovementLine(
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

public sealed record MovementLocation(
    string Address,
    string? City,
    string? PostalCode,
    string Country,
    string? WarehouseId,
    string? LocationId);

public sealed record StockMovementDetail(
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
    MovementLocation ShipFrom,
    MovementLocation ShipTo,
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
    IReadOnlyList<StockMovementLine> Lines);

/// <summary>Movement types of the goods in circulation regime, for the UI selects.</summary>
public static class MovementTypes
{
    public static readonly (string Code, string Label)[] All =
    [
        ("GR", "Guia de remessa"),
        ("GT", "Guia de transporte"),
        ("GA", "Guia de ativos próprios"),
        ("GC", "Guia de consignação"),
        ("GD", "Guia de devolução")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(type => type.Code == code).Label ?? code;
}
