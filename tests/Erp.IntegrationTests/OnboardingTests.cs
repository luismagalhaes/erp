using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Signing up is the one path that creates a company without an admin, and it writes to three
/// tables at once — the company, its subscription and the membership that ties the user to it.
/// A substituted storage cannot show that they land together, or that the second attempt by the
/// same user is refused before anything is written, so it is proven here against a real database.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public class OnboardingTests(SqlServerFixture fixture)
{
    private static int _taxIdCounter = 90_000;

    /// <summary>The run empties every table first, seeded plans included, so each test brings its own.</summary>
    private async Task<Guid> GivenPlanAsync(string billingPeriod = BillingPeriods.Trial, int trialDays = 30)
    {
        await using var scope = fixture.CreateScope();
        var plans = scope.ServiceProvider.GetRequiredService<ISubscriptionPlanService>();

        var plan = await plans.CreateAsync(new CreateSubscriptionPlanRequest(
            $"Plano {Guid.NewGuid():N}"[..20],
            "Criado por um teste",
            billingPeriod == BillingPeriods.Trial ? 0m : 100m,
            billingPeriod,
            trialDays));

        return plan.Id;
    }

    private static SelfServiceCompanyRequest Request(Guid planId) =>
        new(planId,
            $"Empresa {Guid.NewGuid().ToString("N")[..8]}",
            $"5{Interlocked.Increment(ref _taxIdCounter):D8}",
            Address: "Rua Um",
            City: "Braga",
            PostalCode: "4700-000");

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = fixture.CreateScope();
        return await work(scope.ServiceProvider);
    }

    [Fact]
    public async Task Signing_up_creates_the_company_its_subscription_and_the_membership_together()
    {
        var planId = await GivenPlanAsync();
        var userId = $"user-{Guid.NewGuid():N}";

        var company = await InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync(userId, Request(planId)));

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscription = await db.Set<CompanySubscription>()
            .SingleOrDefaultAsync(x => x.CompanyId == company.Id);

        subscription.Should().NotBeNull("the company is worth nothing without a subscription");
        subscription!.PlanId.Should().Be(planId);
        subscription.ExpiresAtUtc.Should().BeCloseTo(subscription.StartedAtUtc.AddDays(30), TimeSpan.FromMinutes(1));

        var membership = await db.Set<UserCompany>()
            .SingleOrDefaultAsync(x => x.CompanyId == company.Id && x.UserId == userId);

        membership.Should().NotBeNull("a company nobody belongs to is unreachable");
        membership!.Role.Should().Be("Owner");
        membership.IsActive.Should().BeTrue();
    }

    /// <summary>
    /// The company still gets everything a company created by an admin gets — the sign-up path
    /// reuses that creation rather than writing a leaner one of its own.
    /// </summary>
    [Fact]
    public async Task Signing_up_gives_the_company_its_default_warehouse_and_eco_fees()
    {
        var planId = await GivenPlanAsync();

        var company = await InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync($"user-{Guid.NewGuid():N}", Request(planId)));

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var warehouses = await db.Set<Warehouse>().Where(x => x.CompanyId == company.Id).ToListAsync();
        warehouses.Should().ContainSingle().Which.IsDefault.Should().BeTrue();

        var ecoFees = await db.Set<EcoFeeType>().Where(x => x.CompanyId == company.Id).ToListAsync();
        ecoFees.Should().NotBeEmpty();
    }

    /// <summary>
    /// Onboarding runs once. Without this, a path that deliberately does not require an admin would
    /// let one account keep creating companies for itself.
    /// </summary>
    [Fact]
    public async Task Signing_up_twice_is_refused_and_writes_nothing_the_second_time()
    {
        var planId = await GivenPlanAsync();
        var userId = $"user-{Guid.NewGuid():N}";

        await InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync(userId, Request(planId)));

        var secondRequest = Request(planId);

        var act = () => InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync(userId, secondRequest));

        await act.Should().ThrowAsync<InvalidOperationException>();

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var companies = await db.Set<Company>().Where(x => x.TaxId == secondRequest.TaxId).ToListAsync();
        companies.Should().BeEmpty("the second company must not exist at all");

        var memberships = await db.Set<UserCompany>().Where(x => x.UserId == userId).ToListAsync();
        memberships.Should().ContainSingle();
    }

    /// <summary>A rejected company leaves nothing behind — not even the subscription row.</summary>
    [Fact]
    public async Task A_company_that_fails_validation_leaves_no_subscription_behind()
    {
        var planId = await GivenPlanAsync();
        var userId = $"user-{Guid.NewGuid():N}";

        var act = () => InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync(userId, Request(planId) with { PostalCode = "not-a-postal-code" }));

        await act.Should().ThrowAsync<ArgumentException>();

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var memberships = await db.Set<UserCompany>().Where(x => x.UserId == userId).ToListAsync();
        memberships.Should().BeEmpty();
    }

    /// <summary>Moving to another package rewrites the row rather than piling up history.</summary>
    [Fact]
    public async Task Buying_another_package_replaces_the_subscription_the_company_had()
    {
        var trialId = await GivenPlanAsync();
        var annualId = await GivenPlanAsync(BillingPeriods.Annual, trialDays: 0);

        var company = await InScopeAsync(services =>
            services.GetRequiredService<IOnboardingService>()
                .CreateCompanyForUserAsync($"user-{Guid.NewGuid():N}", Request(trialId)));

        await InScopeAsync(services =>
            services.GetRequiredService<ICompanySubscriptionService>()
                .AssignAsync(company.Id, new AssignSubscriptionRequest(annualId)));

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var subscriptions = await db.Set<CompanySubscription>()
            .Where(x => x.CompanyId == company.Id)
            .ToListAsync();

        subscriptions.Should().ContainSingle().Which.PlanId.Should().Be(annualId);
    }
}
