namespace Erp.Core.Domain;

/// <summary>
/// A subscription package companies can be on — not scoped to a company, set once for the whole
/// system by a SuperAdmin, the same way <see cref="VatRate"/> is.
/// </summary>
public sealed class SubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>"Trial", "Monthly" or "Annual" — see <see cref="BillingPeriods"/>.</summary>
    public string BillingPeriod { get; set; } = string.Empty;

    /// <summary>How long a "Trial" plan runs before it expires. Unused by any other billing period.</summary>
    public int TrialDays { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>The billing periods a <see cref="SubscriptionPlan"/> can carry.</summary>
public static class BillingPeriods
{
    public const string Trial = "Trial";
    public const string Monthly = "Monthly";
    public const string Annual = "Annual";

    public static readonly string[] All = [Trial, Monthly, Annual];
}
