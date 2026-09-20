using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

public class SubscriptionPlanServiceTests
{
    private readonly ISubscriptionPlanStorage _storage = Substitute.For<ISubscriptionPlanStorage>();

    private SubscriptionPlanService CreateService() => new(_storage);

    [Fact]
    public async Task CreateAsync_saves_the_plan_it_was_given()
    {
        SubscriptionPlan? added = null;
        _storage.When(x => x.AddAsync(Arg.Any<SubscriptionPlan>(), Arg.Any<CancellationToken>()))
            .Do(call => added = call.Arg<SubscriptionPlan>());

        var created = await CreateService().CreateAsync(
            new CreateSubscriptionPlanRequest("  Anual  ", " Tudo incluído ", 299m, BillingPeriods.Annual));

        added.Should().NotBeNull();
        added!.Name.Should().Be("Anual");
        added.Description.Should().Be("Tudo incluído");
        added.Price.Should().Be(299m);
        added.IsActive.Should().BeTrue();
        created.Name.Should().Be("Anual");
    }

    /// <summary>A trial that expires the day it starts would leave the company with nothing.</summary>
    [Fact]
    public async Task CreateAsync_refuses_a_trial_that_does_not_say_how_long_it_runs()
    {
        var act = () => CreateService().CreateAsync(
            new CreateSubscriptionPlanRequest("Free", "", 0m, BillingPeriods.Trial, TrialDays: 0));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("Weekly")]
    [InlineData("")]
    [InlineData("annual")]
    public async Task CreateAsync_refuses_a_billing_period_it_does_not_know(string billingPeriod)
    {
        var act = () => CreateService().CreateAsync(
            new CreateSubscriptionPlanRequest("Plano", "", 10m, billingPeriod));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_refuses_a_negative_price()
    {
        var act = () => CreateService().CreateAsync(
            new CreateSubscriptionPlanRequest("Plano", "", -1m, BillingPeriods.Monthly));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_refuses_a_name_another_plan_already_uses()
    {
        _storage.NameExistsAsync("Anual", null, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateService().CreateAsync(
            new CreateSubscriptionPlanRequest("Anual", "", 299m, BillingPeriods.Annual));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Deactivating is an update: a plan a company is on is never deleted.</summary>
    [Fact]
    public async Task UpdateAsync_can_deactivate_a_plan()
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mensal",
            Price = 29.90m,
            BillingPeriod = BillingPeriods.Monthly,
            IsActive = true
        };

        _storage.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);

        var updated = await CreateService().UpdateAsync(
            plan.Id,
            new UpdateSubscriptionPlanRequest("Mensal", "", 29.90m, BillingPeriods.Monthly, 0, IsActive: false));

        updated!.IsActive.Should().BeFalse();
        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_returns_null_for_a_plan_that_does_not_exist()
    {
        _storage.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SubscriptionPlan?)null);

        var updated = await CreateService().UpdateAsync(
            Guid.NewGuid(),
            new UpdateSubscriptionPlanRequest("Mensal", "", 10m, BillingPeriods.Monthly, 0, true));

        updated.Should().BeNull();
    }
}

public class CompanySubscriptionServiceTests
{
    private readonly ICompanySubscriptionStorage _storage = Substitute.For<ICompanySubscriptionStorage>();
    private readonly ISubscriptionPlanStorage _plans = Substitute.For<ISubscriptionPlanStorage>();
    private readonly ICompanyStorage _companies = Substitute.For<ICompanyStorage>();

    private readonly Guid _companyId = Guid.NewGuid();

    private CompanySubscriptionService CreateService() => new(_storage, _plans, _companies);

    private SubscriptionPlan GivenPlan(string billingPeriod, int trialDays = 0)
    {
        var plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = billingPeriod,
            BillingPeriod = billingPeriod,
            TrialDays = trialDays,
            Price = 10m
        };

        _plans.GetByIdAsync(plan.Id, Arg.Any<CancellationToken>()).Returns(plan);
        _companies.GetByIdAsync(_companyId, Arg.Any<CancellationToken>())
            .Returns(new Company { Id = _companyId, Name = "Alfa" });

        return plan;
    }

    /// <summary>A trial says how long it runs; the rest are a month or a year from the start.</summary>
    [Fact]
    public async Task AssignAsync_expires_a_trial_after_its_own_number_of_days()
    {
        var plan = GivenPlan(BillingPeriods.Trial, trialDays: 30);
        var startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var assigned = await CreateService().AssignAsync(
            _companyId, new AssignSubscriptionRequest(plan.Id, startedAt));

        assigned.ExpiresAtUtc.Should().Be(startedAt.AddDays(30));
    }

    [Fact]
    public async Task AssignAsync_expires_an_annual_plan_a_year_later()
    {
        var plan = GivenPlan(BillingPeriods.Annual);
        var startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var assigned = await CreateService().AssignAsync(
            _companyId, new AssignSubscriptionRequest(plan.Id, startedAt));

        assigned.ExpiresAtUtc.Should().Be(startedAt.AddYears(1));
    }

    /// <summary>A SuperAdmin correcting a subscription by hand keeps the dates they typed.</summary>
    [Fact]
    public async Task AssignAsync_keeps_the_dates_it_was_given()
    {
        var plan = GivenPlan(BillingPeriods.Monthly);
        var startedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        var assigned = await CreateService().AssignAsync(
            _companyId, new AssignSubscriptionRequest(plan.Id, startedAt, expiresAt));

        assigned.StartedAtUtc.Should().Be(startedAt);
        assigned.ExpiresAtUtc.Should().Be(expiresAt);
    }

    [Fact]
    public async Task AssignAsync_refuses_an_expiry_before_the_start()
    {
        var plan = GivenPlan(BillingPeriods.Monthly);
        var startedAt = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        var act = () => CreateService().AssignAsync(
            _companyId, new AssignSubscriptionRequest(plan.Id, startedAt, startedAt.AddDays(-1)));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>One row per company: moving to another package rewrites the current one.</summary>
    [Fact]
    public async Task AssignAsync_replaces_the_subscription_the_company_already_had()
    {
        var plan = GivenPlan(BillingPeriods.Annual);

        var existing = new CompanySubscription
        {
            Id = Guid.NewGuid(),
            CompanyId = _companyId,
            PlanId = Guid.NewGuid(),
            StartedAtUtc = DateTime.UtcNow.AddYears(-1),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        _storage.GetByCompanyIdAsync(_companyId, Arg.Any<CancellationToken>()).Returns(existing);

        var assigned = await CreateService().AssignAsync(_companyId, new AssignSubscriptionRequest(plan.Id));

        assigned.Id.Should().Be(existing.Id);
        existing.PlanId.Should().Be(plan.Id);
        await _storage.DidNotReceive().AddAsync(Arg.Any<CompanySubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignAsync_refuses_a_plan_that_does_not_exist()
    {
        _plans.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((SubscriptionPlan?)null);

        var act = () => CreateService().AssignAsync(_companyId, new AssignSubscriptionRequest(Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
