namespace Erp.FiscalPT.Saft;

/// <summary>A loading or unloading point of a goods movement (ShipFrom / ShipTo).</summary>
public sealed class SaftShippingPoint
{
    public string? WarehouseId { get; init; }

    public string? LocationId { get; init; }

    public SaftAddress Address { get; init; } = new();
}
