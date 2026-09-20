namespace Erp.Core.Domain;

/// <summary>
/// The subscription a company is currently on. One row per company — "buying" a different plan
/// replaces it, there is no history kept of past plans (no payment gateway sits behind this; a
/// SuperAdmin or the company's own owner just records which plan applies and until when).
/// </summary>
public sealed class CompanySubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid PlanId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Computed, not stored — nothing has to flip a status column when the clock ticks past it.</summary>
    public bool IsExpired => ExpiresAtUtc < DateTime.UtcNow;

    public Company Company { get; set; } = null!;

    public SubscriptionPlan Plan { get; set; } = null!;
}
