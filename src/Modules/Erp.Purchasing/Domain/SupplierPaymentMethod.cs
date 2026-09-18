namespace Erp.Purchasing.Domain;

/// <summary>One way the money left. A payment may carry several: part by transfer, part by cheque.</summary>
public sealed class SupplierPaymentMethod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PaymentId { get; set; }

    /// <summary>The same mechanisms as a receipt: NU, CH, TB, ...</summary>
    public string Mechanism { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public SupplierPayment Payment { get; set; } = null!;
}
