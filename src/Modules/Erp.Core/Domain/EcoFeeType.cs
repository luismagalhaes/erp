namespace Erp.Core.Domain;

/// <summary>How the fee is worked out: a flat amount per unit sold, or an amount per kilogram of
/// the article's net weight — the two bases real waste streams use (batteries per unit, oils per
/// kilogram).</summary>
public enum EcoFeeCalculationBasis
{
    PerUnit,
    PerKg
}

/// <summary>
/// An environmental fee — "Ecovalor" — a waste-management entity charges per article under
/// DL 152-D/2017, such as the fee on batteries or on lubricating oils. It is never folded into the
/// price of the article that carries it: the law requires it on its own document line, taxed with
/// VAT like any other line, marked <c>IsEcoFee</c> and built straight from <see cref="Code"/>,
/// <see cref="Description"/> and <see cref="Rate"/> — it needs no catalog article of its own.
/// </summary>
public sealed class EcoFeeType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public EcoFeeCalculationBasis CalculationBasis { get; set; }

    /// <summary>Amount per unit, or per kilogram when <see cref="CalculationBasis"/> is PerKg.</summary>
    public decimal Rate { get; set; }

    /// <summary>The entity the fee is collected for — printed on the document, as the law requires.</summary>
    public string ManagingEntityName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
