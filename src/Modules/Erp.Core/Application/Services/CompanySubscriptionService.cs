using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class CompanySubscriptionService(
    ICompanySubscriptionStorage storage,
    ISubscriptionPlanStorage planStorage,
    ICompanyStorage companyStorage) : ICompanySubscriptionService
{
    public async Task<CompanySubscriptionDto?> GetForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var subscription = await storage.GetActiveByCompanyIdAsync(companyId, cancellationToken);
        if (subscription is null)
            return null;

        var company = await companyStorage.GetByIdAsync(companyId, cancellationToken);

        return Map(subscription, company?.Name ?? string.Empty);
    }

    public async Task<IReadOnlyList<CompanySubscriptionDto>> GetAllForCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await storage.GetAllByCompanyIdAsync(companyId, cancellationToken);
        var company = await companyStorage.GetByIdAsync(companyId, cancellationToken);
        var companyName = company?.Name ?? string.Empty;

        return [.. subscriptions.Select(x => Map(x, companyName))];
    }

    public IQueryable<CompanySubscriptionDto> Query() => storage.Query();

    public async Task<CompanySubscriptionDto> AssignAsync(
        Guid companyId,
        AssignSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await planStorage.GetByIdAsync(request.PlanId, cancellationToken)
            ?? throw new InvalidOperationException($"Subscription plan '{request.PlanId}' does not exist.");

        var company = await companyStorage.GetByIdAsync(companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Company '{companyId}' does not exist.");

        var startedAt = request.StartedAtUtc ?? DateTime.UtcNow;
        var expiresAt = request.ExpiresAtUtc ?? ExpiryFor(plan, startedAt);

        if (expiresAt <= startedAt)
            throw new ArgumentException("A subscription cannot expire before it starts.", nameof(request));

        // Always a new row: a company can be on a free plan today and already have an annual one
        // scheduled to start later, so an existing subscription is never overwritten.
        var subscription = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanId = plan.Id,
            StartedAtUtc = startedAt,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = DateTime.UtcNow
        };

        await storage.AddAsync(subscription, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return new CompanySubscriptionDto(
            subscription.Id,
            companyId,
            company.Name,
            plan.Id,
            plan.Name,
            plan.Price,
            plan.BillingPeriod,
            subscription.StartedAtUtc,
            subscription.ExpiresAtUtc);
    }

    public async Task<bool> RemoveAsync(Guid companyId, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await storage.GetByIdAsync(companyId, subscriptionId, cancellationToken);
        if (subscription is null)
            return false;

        storage.Remove(subscription);
        await storage.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>How long a package runs: a trial says so itself, the rest are a month or a year.</summary>
    private static DateTime ExpiryFor(SubscriptionPlan plan, DateTime startedAt) => plan.BillingPeriod switch
    {
        BillingPeriods.Trial => startedAt.AddDays(plan.TrialDays),
        BillingPeriods.Monthly => startedAt.AddMonths(1),
        BillingPeriods.Annual => startedAt.AddYears(1),
        _ => throw new InvalidOperationException($"Plan '{plan.Name}' has no billing period to compute an expiry from.")
    };

    private static CompanySubscriptionDto Map(CompanySubscription subscription, string companyName) =>
        new(subscription.Id,
            subscription.CompanyId,
            companyName,
            subscription.PlanId,
            subscription.Plan?.Name ?? string.Empty,
            subscription.Plan?.Price ?? 0m,
            subscription.Plan?.BillingPeriod ?? string.Empty,
            subscription.StartedAtUtc,
            subscription.ExpiresAtUtc);
}
