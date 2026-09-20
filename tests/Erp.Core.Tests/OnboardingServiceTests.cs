using Erp.Common;
using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// Sign-up is the one path that creates a company without an admin, so what it refuses matters as
/// much as what it does.
/// </summary>
public class OnboardingServiceTests
{
    private readonly ICompanyAdminService _companies = Substitute.For<ICompanyAdminService>();
    private readonly ICompanySubscriptionService _subscriptions = Substitute.For<ICompanySubscriptionService>();
    private readonly IUserCompanyStorage _memberships = Substitute.For<IUserCompanyStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private const string UserId = "user-1";

    public OnboardingServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<ITransaction>());
    }

    private OnboardingService CreateService() => new(_companies, _subscriptions, _memberships, _unitOfWork);

    private CompanyDetailDto GivenCompanyIsCreated()
    {
        var company = new CompanyDetailDto(
            Guid.NewGuid(), "Alfa", null, "500000001", null, null, null, null, null, "PT", true, DateTime.UtcNow, null);

        _companies.CreateAsync(Arg.Any<CreateCompanyRequest>(), Arg.Any<CancellationToken>()).Returns(company);

        return company;
    }

    private static SelfServiceCompanyRequest Request(Guid planId) =>
        new(planId, "Alfa", "500000001", PostalCode: "4700-000", City: "Braga");

    [Fact]
    public async Task CreateCompanyForUserAsync_makes_the_caller_the_owner_of_the_new_company()
    {
        var company = GivenCompanyIsCreated();

        UserCompany? membership = null;
        _memberships.When(x => x.AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>()))
            .Do(call => membership = call.Arg<UserCompany>());

        await CreateService().CreateCompanyForUserAsync(UserId, Request(Guid.NewGuid()));

        membership.Should().NotBeNull();
        membership!.UserId.Should().Be(UserId);
        membership.CompanyId.Should().Be(company.Id);
        membership.Role.Should().Be("Owner");
        membership.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCompanyForUserAsync_puts_the_company_on_the_plan_that_was_picked()
    {
        var company = GivenCompanyIsCreated();
        var planId = Guid.NewGuid();

        await CreateService().CreateCompanyForUserAsync(UserId, Request(planId));

        await _subscriptions.Received(1).AssignAsync(
            company.Id,
            Arg.Is<AssignSubscriptionRequest>(x => x.PlanId == planId),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Onboarding runs once. Without this, a path that deliberately does not require an admin would
    /// let anyone keep creating companies for themselves.
    /// </summary>
    [Fact]
    public async Task CreateCompanyForUserAsync_refuses_a_user_who_already_belongs_to_a_company()
    {
        _memberships.GetActiveByUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<UserCompany> { new() { UserId = UserId, CompanyId = Guid.NewGuid() } });

        var act = () => CreateService().CreateCompanyForUserAsync(UserId, Request(Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>();

        await _companies.DidNotReceive().CreateAsync(Arg.Any<CreateCompanyRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCompanyForUserAsync_refuses_a_blank_user()
    {
        var act = () => CreateService().CreateCompanyForUserAsync("  ", Request(Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>The company, its subscription and the membership are worth nothing apart.</summary>
    [Fact]
    public async Task CreateCompanyForUserAsync_commits_everything_as_one_transaction()
    {
        var transaction = Substitute.For<ITransaction>();
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(transaction);

        GivenCompanyIsCreated();

        await CreateService().CreateCompanyForUserAsync(UserId, Request(Guid.NewGuid()));

        await transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HasCompanyAsync_is_false_for_an_account_with_no_membership()
    {
        _memberships.GetActiveByUserAsync(UserId, Arg.Any<CancellationToken>()).Returns([]);

        var hasCompany = await CreateService().HasCompanyAsync(UserId);

        hasCompany.Should().BeFalse();
    }
}
