namespace Erp.FiscalPT.Saft;

/// <summary>One way the money moved on a receipt.</summary>
public sealed class SaftPaymentMethod
{
    /// <summary>NU, CH, CD, CC, TB, DE, CO, CS, LC or OU.</summary>
    public string PaymentMechanism { get; init; } = string.Empty;

    public decimal PaymentAmount { get; init; }

    public DateOnly PaymentDate { get; init; }
}
