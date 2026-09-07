namespace Erp.FiscalPT.Saft;

/// <summary>A line of a goods movement document.</summary>
public sealed class SaftStockMovementLine
{
    public int LineNumber { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductDescription { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string UnitOfMeasure { get; init; } = "UN";

    public decimal UnitPrice { get; init; }

    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public SaftTax Tax { get; init; } = new();

    public string? TaxExemptionReason { get; init; }

    public string? TaxExemptionCode { get; init; }
}
