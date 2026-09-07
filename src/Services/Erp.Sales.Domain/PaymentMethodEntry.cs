namespace Erp.Sales.Domain;

/// <summary>
/// One way the money moved, exported as SAF-T PaymentMethod. A receipt may carry several: part
/// in cash, part by card.
/// </summary>
public sealed class PaymentMethodEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    /// <summary>SAF-T PaymentMechanism: NU, CH, CD, CC, TB, ...</summary>
    public string Mechanism { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public Payment Payment { get; set; } = null!;
}
