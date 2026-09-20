using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class SubscriptionPlanService(ISubscriptionPlanStorage storage) : ISubscriptionPlanService
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetAllAsync(
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        var plans = await storage.GetAllAsync(activeOnly, cancellationToken);
        return plans.Select(Map).ToList();
    }

    public IQueryable<SubscriptionPlanDto> Query() => storage.Query();

    public async Task<SubscriptionPlanDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await storage.GetByIdAsync(id, cancellationToken);
        return plan is null ? null : Map(plan);
    }

    public async Task<SubscriptionPlanDto> CreateAsync(
        CreateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = Validate(request.Name, request.Price, request.BillingPeriod, request.TrialDays);

        if (await storage.NameExistsAsync(name, cancellationToken: cancellationToken))
            throw new InvalidOperationException($"A subscription plan named '{name}' already exists.");

        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            Price = request.Price,
            BillingPeriod = request.BillingPeriod.Trim(),
            TrialDays = request.TrialDays,
            IsActive = true
        };

        await storage.AddAsync(plan, cancellationToken);
        await storage.SaveChangesAsync(cancellationToken);

        return Map(plan);
    }

    public async Task<SubscriptionPlanDto?> UpdateAsync(
        Guid id,
        UpdateSubscriptionPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = Validate(request.Name, request.Price, request.BillingPeriod, request.TrialDays);

        var plan = await storage.GetByIdAsync(id, cancellationToken);
        if (plan is null)
            return null;

        if (await storage.NameExistsAsync(name, id, cancellationToken))
            throw new InvalidOperationException($"Another subscription plan already uses the name '{name}'.");

        plan.Name = name;
        plan.Description = request.Description?.Trim() ?? string.Empty;
        plan.Price = request.Price;
        plan.BillingPeriod = request.BillingPeriod.Trim();
        plan.TrialDays = request.TrialDays;
        plan.IsActive = request.IsActive;

        await storage.SaveChangesAsync(cancellationToken);

        return Map(plan);
    }

    /// <summary>Returns the trimmed name, so a caller never has to trim it twice.</summary>
    private static string Validate(string name, decimal price, string billingPeriod, int trialDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (price < 0)
            throw new ArgumentException("A subscription plan cannot cost less than nothing.", nameof(price));

        if (!BillingPeriods.All.Contains(billingPeriod?.Trim(), StringComparer.Ordinal))
            throw new ArgumentException(
                $"'{billingPeriod}' is not a billing period: use {string.Join(", ", BillingPeriods.All)}.",
                nameof(billingPeriod));

        if (trialDays < 0)
            throw new ArgumentException("A trial cannot run for fewer than zero days.", nameof(trialDays));

        // A trial that expires the same day it starts would leave the company with nothing.
        if (string.Equals(billingPeriod!.Trim(), BillingPeriods.Trial, StringComparison.Ordinal) && trialDays == 0)
            throw new ArgumentException("A trial plan has to say how many days it runs for.", nameof(trialDays));

        return name.Trim();
    }

    private static SubscriptionPlanDto Map(SubscriptionPlan plan) =>
        new(plan.Id, plan.Name, plan.Description, plan.Price, plan.BillingPeriod, plan.TrialDays, plan.IsActive);
}
