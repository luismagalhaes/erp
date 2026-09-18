namespace Erp.FiscalPT.Saft;

/// <summary>Totals of a document. GrossTotal has to equal NetTotal plus TaxPayable.</summary>
public sealed class SaftDocumentTotals
{
    public decimal TaxPayable { get; init; }

    public decimal NetTotal { get; init; }

    public decimal GrossTotal { get; init; }

    /// <summary>How the document was paid when issued, written as Payment. Only a fatura-recibo has any.</summary>
    public IReadOnlyList<SaftPaymentMethod> Payments { get; init; } = [];
}
