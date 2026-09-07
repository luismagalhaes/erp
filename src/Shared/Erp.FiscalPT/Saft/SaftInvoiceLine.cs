namespace Erp.FiscalPT.Saft;

/// <summary>A line of an invoicing document.</summary>
public sealed class SaftInvoiceLine
{
    public int LineNumber { get; init; }

    /// <summary>
    /// Number of the delivery note this line invoices, written as OrderReferences/OriginatingON.
    /// </summary>
    public string? OriginatingOn { get; init; }

    /// <summary>Date of that delivery note, written as OrderReferences/OrderDate.</summary>
    public DateOnly? OrderDate { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductDescription { get; init; } = string.Empty;

    public decimal Quantity { get; init; }

    public string UnitOfMeasure { get; init; } = "UN";

    public decimal UnitPrice { get; init; }

    /// <summary>Date the tax became due; the document date for goods delivered on issue.</summary>
    public DateOnly TaxPointDate { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>Line amount without tax. Written as DebitAmount or CreditAmount by the document type.</summary>
    public decimal Amount { get; init; }

    public SaftTax Tax { get; init; } = new();

    /// <summary>
    /// Number of the document this line corrects, for a credit or debit note. Written as
    /// References/Reference, which the law requires on a rectifying document.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>Why the document is being corrected, written as References/Reason.</summary>
    public string? ReferenceReason { get; init; }

    /// <summary>Required by law whenever the line carries no tax.</summary>
    public string? TaxExemptionReason { get; init; }

    /// <summary>Code from the tax authority's exemption table, e.g. M07.</summary>
    public string? TaxExemptionCode { get; init; }
}
