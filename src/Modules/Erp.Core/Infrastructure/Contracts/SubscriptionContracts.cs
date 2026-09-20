namespace Erp.Core.Infrastructure.Contracts;

/// <summary>
/// A subscription package as the listings and the sign-up page see it. Init properties for the
/// same reason as the other listing DTOs: EF Core only maps members over an object initializer,
/// which is what keeps the OData $filter and $orderby applied afterwards translatable to SQL.
/// </summary>
public sealed record SubscriptionPlanDto
{
    public SubscriptionPlanDto()
    {
    }

    public SubscriptionPlanDto(
        Guid id,
        string name,
        string description,
        decimal price,
        string billingPeriod,
        int trialDays,
        bool isActive)
    {
        Id = id;
        Name = name;
        Description = description;
        Price = price;
        BillingPeriod = billingPeriod;
        TrialDays = trialDays;
        IsActive = isActive;
    }

    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }

    /// <summary>"Trial", "Monthly" or "Annual".</summary>
    public string BillingPeriod { get; init; } = string.Empty;

    /// <summary>How long a "Trial" plan runs. Zero for every other billing period.</summary>
    public int TrialDays { get; init; }

    public bool IsActive { get; init; }
}

public sealed record CreateSubscriptionPlanRequest(
    string Name,
    string Description,
    decimal Price,
    string BillingPeriod,
    int TrialDays = 0);

public sealed record UpdateSubscriptionPlanRequest(
    string Name,
    string Description,
    decimal Price,
    string BillingPeriod,
    int TrialDays,
    bool IsActive);

/// <summary>
/// What a company is subscribed to, flattened for the backoffice listing: the company and the plan
/// are named here so the grid does not have to join anything of its own.
/// </summary>
public sealed record CompanySubscriptionDto
{
    public CompanySubscriptionDto()
    {
    }

    public CompanySubscriptionDto(
        Guid id,
        Guid companyId,
        string companyName,
        Guid planId,
        string planName,
        decimal planPrice,
        string billingPeriod,
        DateTime startedAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        CompanyId = companyId;
        CompanyName = companyName;
        PlanId = planId;
        PlanName = planName;
        PlanPrice = planPrice;
        BillingPeriod = billingPeriod;
        StartedAtUtc = startedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public Guid PlanId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public decimal PlanPrice { get; init; }
    public string BillingPeriod { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

/// <summary>
/// Moves a company onto a plan. <paramref name="StartedAtUtc"/> and <paramref name="ExpiresAtUtc"/>
/// are optional: left out, they are taken from the plan itself (now, and now plus the plan's own
/// length). A SuperAdmin correcting a subscription by hand is what fills them in.
/// </summary>
public sealed record AssignSubscriptionRequest(
    Guid PlanId,
    DateTime? StartedAtUtc = null,
    DateTime? ExpiresAtUtc = null);

/// <summary>Whether the caller still has to go through sign-up.</summary>
public sealed record OnboardingStatus(bool HasCompany);

/// <summary>
/// Everything a user with no company needs to send once: the plan they picked, and the company to
/// create for them. The user is taken from the caller's own token, never from the body.
/// </summary>
public sealed record SelfServiceCompanyRequest(
    Guid PlanId,
    string Name,
    string TaxId,
    string? LegalName = null,
    string? Email = null,
    string? Phone = null,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT");
