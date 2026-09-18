using Erp.FiscalPT;

namespace Erp.Purchasing.Domain;

/// <summary>
/// Money paid to a supplier, and which of their documents it settles. The mirror of a receipt on
/// the sales side — with one difference that shapes everything else: <b>this is not a fiscal
/// document</b>. The receipt for this money is the supplier's to issue, so there is no series, no
/// signature and no SAF-T here, and the number is ours alone.
/// </summary>
/// <remarks>
/// It is still not rewritten once recorded. What it settled is read by every calculation of what a
/// supplier is owed, so a correction is a void followed by a new payment, never an edit.
/// </remarks>
public sealed class SupplierPayment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid CompanyId { get; private set; }

    /// <summary>The supplier from Erp.Core. No physical foreign key — it belongs to another module.</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Our number, e.g. "PAG2026/3". No fiscal meaning.</summary>
    public string Number { get; private set; } = string.Empty;

    public DateOnly PaymentDate { get; private set; }

    public SupplierPaymentStatus Status { get; private set; } = SupplierPaymentStatus.Recorded;

    public SupplierSnapshot Supplier { get; private set; } = null!;

    public string? Description { get; private set; }

    /// <summary>
    /// Money that left: the documents settled less the credit notes used, and the sum of the payment
    /// methods. Zero when the credit covers the invoices entirely.
    /// </summary>
    public decimal Total { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public DateTime? VoidedAtUtc { get; private set; }

    public string? CreatedByUserId { get; private set; }

    public string? VoidedByUserId { get; private set; }

    public string? VoidReason { get; private set; }

    public byte[]? RowVersion { get; set; }

    public ICollection<SupplierPaymentLine> Lines { get; private set; } = [];

    public ICollection<SupplierPaymentMethod> Methods { get; private set; } = [];

    public bool IsVoided => Status == SupplierPaymentStatus.Voided;

    /// <summary>Required by EF Core.</summary>
    private SupplierPayment()
    {
    }

    /// <summary>
    /// Builds a payment. The caller has already checked that each document still owes what is
    /// applied to it; what is checked here is that the payment agrees with itself.
    /// </summary>
    public static SupplierPayment Create(
        Guid companyId,
        Guid supplierId,
        SupplierSnapshot supplier,
        string number,
        DateOnly paymentDate,
        IReadOnlyList<SupplierPaymentLine> lines,
        IReadOnlyList<SupplierPaymentMethod> methods,
        string? description = null,
        string? createdByUserId = null)
    {
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(methods);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);

        // A credit note only discounts: on its own there is nothing to pay.
        if (!lines.Any(line => !line.IsCredit))
            throw new ArgumentException("A payment must settle at least one document.", nameof(lines));

        if (lines.Any(line => line.AppliedAmount <= 0))
            throw new ArgumentException("Every settled document must take a positive amount.", nameof(lines));

        if (methods.Any(method => method.Amount <= 0))
            throw new ArgumentException("A payment method must carry a positive amount.", nameof(methods));

        var total = FiscalRounding.Amount(lines.Sum(line => line.SignedAmount));

        if (total < 0)
        {
            throw new ArgumentException(
                $"The credit notes used exceed the documents settled by {-total:0.00}; use less of the credit.",
                nameof(lines));
        }

        // Credit that covers the invoices entirely moves no money, so there is nothing to record
        // about how it moved.
        if (total > 0 && methods.Count == 0)
            throw new ArgumentException("A payment must record how the money was paid.", nameof(methods));

        var methodsTotal = FiscalRounding.Amount(methods.Sum(method => method.Amount));

        if (methodsTotal != total)
        {
            throw new ArgumentException(
                $"The payment methods add up to {methodsTotal:0.00} but the payment settles {total:0.00}.",
                nameof(methods));
        }

        var payment = new SupplierPayment
        {
            CompanyId = companyId,
            SupplierId = supplierId,
            Supplier = supplier,
            Number = number,
            PaymentDate = paymentDate,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Total = total,
            CreatedByUserId = createdByUserId
        };

        var lineNumber = 1;

        foreach (var line in lines)
        {
            line.PaymentId = payment.Id;
            line.LineNumber = lineNumber++;
            line.AppliedAmount = FiscalRounding.Amount(line.AppliedAmount);
            payment.Lines.Add(line);
        }

        foreach (var method in methods)
        {
            method.PaymentId = payment.Id;
            method.Amount = FiscalRounding.Amount(method.Amount);
            payment.Methods.Add(method);
        }

        return payment;
    }

    /// <summary>
    /// Strikes the payment out. The row stays, so the gap in our numbering is explainable, and the
    /// documents it settled go back to being owed.
    /// </summary>
    public void Void(string reason, string? userId, DateTime voidedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (IsVoided)
            throw new InvalidOperationException($"Payment '{Number}' is already voided.");

        Status = SupplierPaymentStatus.Voided;
        VoidReason = reason.Trim();
        VoidedByUserId = userId;
        VoidedAtUtc = voidedAtUtc;
    }
}
