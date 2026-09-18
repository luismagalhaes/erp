namespace Erp.Main.Models.Sales;

public sealed record MovementLocationRequest(
    string Address,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    string? WarehouseId = null,
    string? LocationId = null);

public sealed record MovementPartyRequest(string? TaxId, string Name, bool IsSupplier = false);

/// <param name="UnitPrice">Price before the line discount.</param>
public sealed record CreateStockMovementLineRequest(
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    decimal UnitPrice,
    string TaxCode,
    decimal TaxPercentage,
    string UnitOfMeasure = "UN",
    string TaxCountryRegion = "PT",
    decimal DiscountPercentage = 0m,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);

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
    string? Comments = null,
    Guid? WarehouseId = null);

public sealed record VoidStockMovementRequest(string Reason);

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

/// <param name="UnitPrice">Price before the line discount.</param>
/// <param name="LineAmount">Line total without VAT, after the discount.</param>
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
    decimal TaxAmount,
    decimal DiscountPercentage = 0m,
    decimal DiscountAmount = 0m,
    string? TaxExemptionCode = null,
    string? TaxExemptionReason = null,
    bool IsEcoFee = false,
    int? EcoFeeForLineNumber = null);

/// <summary>The VAT of a goods movement, grouped by rate.</summary>
public sealed record MovementTax(
    string TaxCountryRegion,
    string TaxCode,
    decimal TaxPercentage,
    decimal TaxableBase,
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
    IReadOnlyList<StockMovementLine> Lines,
    decimal GrossLinesTotal = 0m,
    decimal DiscountTotal = 0m,
    IReadOnlyList<MovementTax>? Taxes = null);

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
