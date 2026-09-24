using Erp.Identity.Application.Handlers;
using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Erp.Identity.Domain.Application;
using Erp.Identity.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Identity.Tests.Application;

public class OnboardingRequestServiceTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private readonly IOnboardingRequestStorage _requests = Substitute.For<IOnboardingRequestStorage>();
    private readonly IUserStorage _users = Substitute.For<IUserStorage>();

    private OnboardingRequestService CreateService() => new(_requests, _users);

    private static InviteToCompanyRequest Invite(string email = "Rui@Example.com") =>
        new(email, CompanyId, "Alfa", Constants.Roles.User, "ana@alfa.pt");

    private static UserEditItem User(string id, string email, bool emailConfirmed = true, params string[] roles) =>
        new(id, email, "Rui", true, emailConfirmed, roles, "pt-PT");

    [Fact]
    public async Task InviteAsync_raises_a_pending_request_for_an_email_with_no_account()
    {
        OnboardingRequest? added = null;
        _requests.When(x => x.AddAsync(Arg.Any<OnboardingRequest>(), Arg.Any<CancellationToken>()))
            .Do(call => added = call.Arg<OnboardingRequest>());

        var result = await CreateService().InviteAsync(Invite());

        result.UserExists.Should().BeFalse();
        result.Request.Should().NotBeNull();
        added.Should().NotBeNull();
        added!.Email.Should().Be("rui@example.com");
        added.CompanyId.Should().Be(CompanyId);
        added.Status.Should().Be(Constants.OnboardingStatuses.Pending);
    }

    [Fact]
    public async Task InviteAsync_raises_nothing_for_an_email_that_already_has_an_account_and_grants_it_the_user_role()
    {
        _users.FindByEmailAsync("rui@example.com", Arg.Any<CancellationToken>())
            .Returns(User("user-9", "rui@example.com"));

        var result = await CreateService().InviteAsync(Invite());

        result.UserExists.Should().BeTrue();
        result.ExistingUserId.Should().Be("user-9");
        result.Request.Should().BeNull();
        await _requests.DidNotReceive().AddAsync(Arg.Any<OnboardingRequest>(), Arg.Any<CancellationToken>());
        await _users.Received(1).UpdateUserRolesAsync(
            "user-9", Arg.Is<IReadOnlyCollection<string>>(roles => roles.Contains(Constants.Roles.User)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAsync_keeps_the_roles_an_existing_account_already_holds()
    {
        _users.FindByEmailAsync("rui@example.com", Arg.Any<CancellationToken>())
            .Returns(User("user-9", "rui@example.com", true, Constants.Roles.SuperAdmin));

        await CreateService().InviteAsync(Invite());

        await _users.Received(1).UpdateUserRolesAsync(
            "user-9",
            Arg.Is<IReadOnlyCollection<string>>(roles =>
                roles.Contains(Constants.Roles.SuperAdmin) && roles.Contains(Constants.Roles.User)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InviteAsync_resends_instead_of_duplicating_a_pending_request()
    {
        var pending = new OnboardingRequest
        {
            Email = "rui@example.com",
            CompanyId = CompanyId,
            CompanyName = "Alfa",
            Role = Constants.Roles.User,
            Status = Constants.OnboardingStatuses.Pending,
            LastSentAtUtc = DateTime.UtcNow.AddDays(-3)
        };
        _requests.GetPendingAsync("rui@example.com", CompanyId, Arg.Any<CancellationToken>()).Returns(pending);

        var result = await CreateService().InviteAsync(Invite());

        result.Request!.Id.Should().Be(pending.Id);
        pending.LastSentAtUtc.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        await _requests.DidNotReceive().AddAsync(Arg.Any<OnboardingRequest>(), Arg.Any<CancellationToken>());
        await _requests.Received(1).UpdateAsync(pending, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task InviteAsync_refuses_an_empty_email(string email)
    {
        var act = () => CreateService().InviteAsync(Invite(email));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>Nobody may claim an invitation by signing up with an address they never confirmed.</summary>
    [Fact]
    public async Task GetPendingForUserAsync_returns_nothing_until_the_email_is_confirmed()
    {
        _users.GetUserAsync("user-9", Arg.Any<CancellationToken>())
            .Returns(User("user-9", "rui@example.com", emailConfirmed: false));

        var pending = await CreateService().GetPendingForUserAsync("user-9");

        pending.Should().BeEmpty();
        await _requests.DidNotReceive().GetPendingByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPendingForUserAsync_matches_the_confirmed_email_of_the_account()
    {
        _users.GetUserAsync("user-9", Arg.Any<CancellationToken>()).Returns(User("user-9", "Rui@Example.com"));
        _requests.GetPendingByEmailAsync("rui@example.com", Arg.Any<CancellationToken>()).Returns(
        [
            new OnboardingRequest { Email = "rui@example.com", CompanyId = CompanyId, CompanyName = "Alfa", Role = "User", Status = "Pending" }
        ]);

        var pending = await CreateService().GetPendingForUserAsync("user-9");

        pending.Should().ContainSingle().Which.CompanyId.Should().Be(CompanyId);
    }

    [Fact]
    public async Task CompleteAsync_settles_a_pending_request_and_grants_the_user_role()
    {
        var request = new OnboardingRequest { Email = "rui@example.com", Status = Constants.OnboardingStatuses.Pending };
        _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _users.GetUserAsync("user-9", Arg.Any<CancellationToken>()).Returns(User("user-9", "rui@example.com"));

        var completed = await CreateService().CompleteAsync(request.Id, "user-9");

        completed.Should().BeTrue();
        request.Status.Should().Be(Constants.OnboardingStatuses.Completed);
        request.CompletedUserId.Should().Be("user-9");
        await _users.Received(1).UpdateUserRolesAsync(
            "user-9", Arg.Is<IReadOnlyCollection<string>>(roles => roles.Contains(Constants.Roles.User)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_does_not_settle_a_request_twice()
    {
        var request = new OnboardingRequest { Status = Constants.OnboardingStatuses.Completed };
        _requests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        (await CreateService().CompleteAsync(request.Id, "user-9")).Should().BeFalse();

        await _requests.DidNotReceive().UpdateAsync(Arg.Any<OnboardingRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_cancels_only_a_pending_request()
    {
        var pending = new OnboardingRequest { Status = Constants.OnboardingStatuses.Pending };
        var done = new OnboardingRequest { Status = Constants.OnboardingStatuses.Completed };
        _requests.GetByIdAsync(pending.Id, Arg.Any<CancellationToken>()).Returns(pending);
        _requests.GetByIdAsync(done.Id, Arg.Any<CancellationToken>()).Returns(done);

        (await CreateService().CancelAsync(pending.Id)).Should().BeTrue();
        (await CreateService().CancelAsync(done.Id)).Should().BeFalse();

        pending.Status.Should().Be(Constants.OnboardingStatuses.Cancelled);
        done.Status.Should().Be(Constants.OnboardingStatuses.Completed);
    }
}
