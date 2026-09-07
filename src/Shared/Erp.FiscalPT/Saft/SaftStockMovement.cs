namespace Erp.FiscalPT.Saft;

/// <summary>A goods movement document, exported under SourceDocuments/MovementOfGoods.</summary>
public sealed class SaftStockMovement
{
    public string DocumentNumber { get; init; } = string.Empty;

    public string Atcud { get; init; } = string.Empty;

    public SaftDocumentStatus DocumentStatus { get; init; } = new();

    public string Hash { get; init; } = string.Empty;

    public string HashControl { get; init; } = string.Empty;

    public int Period { get; init; }

    public DateOnly MovementDate { get; init; }

    /// <summary>GR, GT, GA, GC or GD.</summary>
    public string MovementType { get; init; } = string.Empty;

    public DateTime SystemEntryDate { get; init; }

    /// <summary>Counterparty identifier; written as SupplierID when the party is a supplier.</summary>
    public string PartyId { get; init; } = string.Empty;

    public bool PartyIsSupplier { get; init; }

    public string SourceId { get; init; } = SaftConstants.Unknown;

    public string? MovementComments { get; init; }

    public SaftShippingPoint ShipTo { get; init; } = new();

    public SaftShippingPoint ShipFrom { get; init; } = new();

    public DateTime MovementStartTime { get; init; }

    public DateTime? MovementEndTime { get; init; }

    /// <summary>Code the tax authority returned when the transport was communicated.</summary>
    public string? AtDocCodeId { get; init; }

    public IReadOnlyList<SaftStockMovementLine> Lines { get; init; } = [];

    public SaftDocumentTotals Totals { get; init; } = new();
}
