namespace Erp.Main.Models.Core;

/// <summary>Mirrors Erp.Core's SubscriptionPlanDto, the API's JSON shape for a package.</summary>
public sealed record SubscriptionPlan(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string BillingPeriod,
    int TrialDays,
    bool IsActive);

/// <summary>The billing periods a plan can carry, mirroring Erp.Core's BillingPeriods.</summary>
public static class BillingPeriods
{
    public const string Trial = "Trial";
    public const string Monthly = "Monthly";
    public const string Annual = "Annual";

    public static readonly string[] All = [Trial, Monthly, Annual];
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

/// <summary>Mirrors Erp.Core's CompanySubscriptionDto: what a company is on, and until when.</summary>
public sealed record CompanySubscription(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    Guid PlanId,
    string PlanName,
    decimal PlanPrice,
    string BillingPeriod,
    DateTime StartedAtUtc,
    DateTime ExpiresAtUtc)
{
    public bool IsExpired => ExpiresAtUtc < DateTime.UtcNow;
}

public sealed record AssignSubscriptionRequest(
    Guid PlanId,
    DateTime? StartedAtUtc = null,
    DateTime? ExpiresAtUtc = null);

/// <summary>A company subscribing itself to a plan, with no admin involved.</summary>
public sealed record SelfServiceSubscribeRequest(Guid PlanId);

/// <summary>Whether the signed-in user still has to go through sign-up.</summary>
public sealed record OnboardingStatus(bool HasCompany);

/// <summary>The package picked plus the company to create, sent once by the sign-up page.</summary>
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
