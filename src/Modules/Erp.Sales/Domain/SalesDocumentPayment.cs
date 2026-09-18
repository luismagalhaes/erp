namespace Erp.Sales.Domain;

/// <summary>
/// How a fatura-recibo was paid, exported as SAF-T DocumentTotals/Payment. A fatura-recibo is
/// paid at the moment it is issued, so it records the money the way a receipt does.
/// </summary>
public sealed class SalesDocumentPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    /// <summary>SAF-T PaymentMechanism: NU, CH, CD, CC, TB, ...</summary>
    public string Mechanism { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public SalesDocument Document { get; set; } = null!;
}
