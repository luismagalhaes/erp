using Erp.Common;
using Erp.Core.Application.Services;
using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Dependencies.IdentityOnboarding;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// A company managing its own people: who it adds straight away, who it has to invite, and what it
/// refuses to leave behind.
/// </summary>
public class CompanyMemberServiceTests
{
    private readonly IUserCompanyStorage _memberships = Substitute.For<IUserCompanyStorage>();
    private readonly ICompanyStorage _companies = Substitute.For<ICompanyStorage>();
    private readonly IIdentityOnboardingClient _identity = Substitute.For<IIdentityOnboardingClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Company _company = new() { Id = Guid.NewGuid(), Name = "Alfa", TaxId = "500000001" };

    public CompanyMemberServiceTests()
    {
        // A bare Substitute.For<IUnitOfWork>() never calls the action it was given.
        _unitOfWork.RunUnrestrictedAsync(Arg.Any<Func<Task>>()).Returns(call => call.Arg<Func<Task>>()());

        _companies.GetByIdAsync(_company.Id, Arg.Any<CancellationToken>()).Returns(_company);
        _memberships.CompanyExistsAsync(_company.Id, Arg.Any<CancellationToken>()).Returns(true);
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([]);
    }

    private CompanyMemberService CreateService() =>
        new(_memberships, _companies, _identity, _unitOfWork, NullLogger<CompanyMemberService>.Instance);

    private UserCompany Membership(string userId, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        CompanyId = _company.Id,
        Company = _company,
        Role = Constants.Roles.User,
        IsActive = isActive
    };

    private static IdentityOnboardingRequestDto Request(Guid companyId, string email = "rui@example.com", string status = "Pending") =>
        new(Guid.NewGuid(), email, companyId, "Alfa", Constants.Roles.User, "ana@alfa.pt", status, DateTime.UtcNow, DateTime.UtcNow, null);

    [Fact]
    public async Task AddAsync_associates_an_email_that_already_has_an_account()
    {
        _identity.InviteAsync(Arg.Any<IdentityInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityInviteResult("user-9", null));

        UserCompany? added = null;
        _memberships.When(x => x.AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                added = call.Arg<UserCompany>();
                added.Company = _company;
                _memberships.GetByIdAsync(added.Id, Arg.Any<CancellationToken>()).Returns(added);
            });

        var result = await CreateService().AddAsync(_company.Id, new AddCompanyMemberRequest("rui@example.com"), "Ana");

        result.Outcome.Should().Be(AddCompanyMemberOutcomes.Added);
        result.Member.Should().NotBeNull();
        added.Should().NotBeNull();
        added!.UserId.Should().Be("user-9");
        added.CompanyId.Should().Be(_company.Id);
        added.Role.Should().Be(Constants.Roles.User);
    }

    [Fact]
    public async Task AddAsync_invites_an_email_nobody_has_signed_up_with_and_adds_no_membership()
    {
        _identity.InviteAsync(Arg.Any<IdentityInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityInviteResult(null, Request(_company.Id)));

        var result = await CreateService().AddAsync(_company.Id, new AddCompanyMemberRequest(" rui@example.com ", "pt-PT"), "Ana");

        result.Outcome.Should().Be(AddCompanyMemberOutcomes.Invited);
        result.Invitation.Should().NotBeNull();
        result.Member.Should().BeNull();
        await _memberships.DidNotReceive().AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>());

        await _identity.Received(1).InviteAsync(
            Arg.Is<IdentityInviteRequest>(x =>
                x.Email == "rui@example.com"
                && x.CompanyId == _company.Id
                && x.CompanyName == "Alfa"
                && x.InvitedByEmail == "Ana"
                && x.Culture == "pt-PT"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task AddAsync_refuses_an_email_that_is_not_one(string email)
    {
        var act = () => CreateService().AddAsync(_company.Id, new AddCompanyMemberRequest(email), null);

        await act.Should().ThrowAsync<ArgumentException>();
        await _identity.DidNotReceive().InviteAsync(Arg.Any<IdentityInviteRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_refuses_an_account_that_already_belongs_to_the_company()
    {
        _identity.InviteAsync(Arg.Any<IdentityInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityInviteResult("user-9", null));
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([Membership("user-9")]);

        var act = () => CreateService().AddAsync(_company.Id, new AddCompanyMemberRequest("rui@example.com"), null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Someone removed earlier and added again gets the same row back, not a duplicate.</summary>
    [Fact]
    public async Task AddAsync_reactivates_a_member_that_was_removed_from_the_company_before()
    {
        var previous = Membership("user-9", isActive: false);
        _identity.InviteAsync(Arg.Any<IdentityInviteRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IdentityInviteResult("user-9", null));
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([previous]);
        _memberships.GetByIdAsync(previous.Id, Arg.Any<CancellationToken>()).Returns(previous);

        var result = await CreateService().AddAsync(_company.Id, new AddCompanyMemberRequest("rui@example.com"), null);

        result.Outcome.Should().Be(AddCompanyMemberOutcomes.Added);
        previous.IsActive.Should().BeTrue();
        await _memberships.DidNotReceive().AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveAsync_refuses_to_remove_the_last_active_member()
    {
        var only = Membership("user-1");
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([only]);

        var act = () => CreateService().RemoveAsync(_company.Id, only.Id);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _memberships.DidNotReceive().Remove(Arg.Any<UserCompany>());
    }

    [Fact]
    public async Task RemoveAsync_removes_a_member_when_others_remain()
    {
        var first = Membership("user-1");
        var second = Membership("user-2");
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([first, second]);
        _memberships.GetByIdAsync(second.Id, Arg.Any<CancellationToken>()).Returns(second);

        var removed = await CreateService().RemoveAsync(_company.Id, second.Id);

        removed.Should().BeTrue();
        _memberships.Received(1).Remove(second);
    }

    /// <summary>The id of another company's membership must look like it does not exist.</summary>
    [Fact]
    public async Task RemoveAsync_does_not_find_a_membership_of_another_company()
    {
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([Membership("user-1"), Membership("user-2")]);

        var removed = await CreateService().RemoveAsync(_company.Id, Guid.NewGuid());

        removed.Should().BeFalse();
        _memberships.DidNotReceive().Remove(Arg.Any<UserCompany>());
    }

    [Fact]
    public async Task CancelInvitationAsync_only_cancels_a_pending_request_of_this_company()
    {
        var mine = Request(_company.Id);
        _identity.GetForCompanyAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([mine]);
        _identity.CancelAsync(mine.Id, Arg.Any<CancellationToken>()).Returns(true);

        (await CreateService().CancelInvitationAsync(_company.Id, mine.Id)).Should().BeTrue();
        (await CreateService().CancelInvitationAsync(_company.Id, Guid.NewGuid())).Should().BeFalse();

        await _identity.Received(1).CancelAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimInvitationsAsync_turns_a_waiting_invitation_into_a_membership_and_settles_it()
    {
        var invitation = Request(_company.Id);
        _identity.GetPendingForUserAsync("user-9", Arg.Any<CancellationToken>()).Returns([invitation]);

        UserCompany? added = null;
        _memberships.When(x => x.AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                added = call.Arg<UserCompany>();
                added.Company = _company;
                _memberships.GetByIdAsync(added.Id, Arg.Any<CancellationToken>()).Returns(added);
            });

        var claimed = await CreateService().ClaimInvitationsAsync("user-9");

        claimed.Should().Be(1);
        added.Should().NotBeNull();
        added!.UserId.Should().Be("user-9");
        added.CompanyId.Should().Be(_company.Id);
        await _identity.Received(1).CompleteAsync(invitation.Id, "user-9", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimInvitationsAsync_does_nothing_when_no_invitation_is_waiting()
    {
        _identity.GetPendingForUserAsync("user-9", Arg.Any<CancellationToken>()).Returns([]);

        (await CreateService().ClaimInvitationsAsync("user-9")).Should().Be(0);

        await _memberships.DidNotReceive().AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>());
    }

    /// <summary>One invitation that cannot be settled must not stop the ones after it.</summary>
    [Fact]
    public async Task ClaimInvitationsAsync_carries_on_when_one_invitation_fails()
    {
        var failing = Request(_company.Id, "a@example.com");
        var fine = Request(_company.Id, "b@example.com");
        _identity.GetPendingForUserAsync("user-9", Arg.Any<CancellationToken>()).Returns([failing, fine]);
        _identity.CompleteAsync(failing.Id, "user-9", Arg.Any<CancellationToken>()).Returns<Task<bool>>(_ => throw new HttpRequestException());

        _memberships.When(x => x.AddAsync(Arg.Any<UserCompany>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var membership = call.Arg<UserCompany>();
                membership.Company = _company;
                _memberships.GetByIdAsync(membership.Id, Arg.Any<CancellationToken>()).Returns(membership);
            });

        await CreateService().ClaimInvitationsAsync("user-9");

        await _identity.Received(1).CompleteAsync(fine.Id, "user-9", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_still_lists_the_members_when_the_identity_host_cannot_be_reached()
    {
        _memberships.GetAllAsync(_company.Id, Arg.Any<CancellationToken>()).Returns([Membership("user-1")]);
        _identity.GetForCompanyAsync(_company.Id, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityOnboardingRequestDto>>>(_ => throw new HttpRequestException());

        var members = await CreateService().GetAsync(_company.Id);

        members.Members.Should().HaveCount(1);
        members.Invitations.Should().BeEmpty();
        members.InvitationsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_lists_only_pending_invitations()
    {
        _identity.GetForCompanyAsync(_company.Id, Arg.Any<CancellationToken>())
            .Returns([Request(_company.Id, "a@example.com"), Request(_company.Id, "b@example.com", "Completed")]);

        var members = await CreateService().GetAsync(_company.Id);

        members.Invitations.Should().ContainSingle().Which.Email.Should().Be("a@example.com");
        members.InvitationsAvailable.Should().BeTrue();
    }
}
