using System.Reflection;
using System.Security.Claims;
using Erp.Api.Security;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NSubstitute;

namespace Erp.Api.Tests;

/// <summary>
/// A token only proves who signed in, not which companies they belong to. Before this filter,
/// nothing stopped a caller with a valid Read or Write scope from asking for any tenant's data by
/// changing the companyId it sent — these tests pin down that the filter actually closes that gap,
/// case by case.
/// </summary>
public class RequireCompanyAccessFilterTests
{
    private readonly IUserCompanyService _userCompanyService = Substitute.For<IUserCompanyService>();
    private const string UserId = "user-1";
    private static readonly Guid CompanyId = Guid.NewGuid();

    private RequireCompanyAccessFilter CreateFilter() => new(_userCompanyService);

    [Fact]
    public async Task Denies_a_companyId_the_caller_does_not_belong_to()
    {
        _userCompanyService.CanAccessCompanyAsync(UserId, CompanyId, Arg.Any<CancellationToken>()).Returns(false);

        var context = BuildContext(nameof(FakeController.Scoped), new Dictionary<string, object?> { ["companyId"] = CompanyId });

        await CreateFilter().OnActionExecutionAsync(context, NextNotCalled());

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Allows_a_companyId_the_caller_belongs_to()
    {
        _userCompanyService.CanAccessCompanyAsync(UserId, CompanyId, Arg.Any<CancellationToken>()).Returns(true);

        var context = BuildContext(nameof(FakeController.Scoped), new Dictionary<string, object?> { ["companyId"] = CompanyId });
        var called = false;

        await CreateFilter().OnActionExecutionAsync(context, Next(() => called = true));

        called.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    /// <summary>A SuperAdmin manages every tenant, the same reach CompaniesController already
    /// grants under Policies.Admin, so the membership table is never even consulted.</summary>
    [Fact]
    public async Task Lets_a_SuperAdmin_through_without_checking_membership()
    {
        var context = BuildContext(
            nameof(FakeController.Scoped),
            new Dictionary<string, object?> { ["companyId"] = CompanyId },
            role: Constants.Roles.SuperAdmin);
        var called = false;

        await CreateFilter().OnActionExecutionAsync(context, Next(() => called = true));

        called.Should().BeTrue();
        await _userCompanyService.DidNotReceive().CanAccessCompanyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Nothing to police when the action never took a companyId in the first place
    /// (e.g. VAT rates, which are not company scoped).</summary>
    [Fact]
    public async Task Lets_an_action_with_no_companyId_through_unchecked()
    {
        var context = BuildContext(nameof(FakeController.Scoped), new Dictionary<string, object?>());
        var called = false;

        await CreateFilter().OnActionExecutionAsync(context, Next(() => called = true));

        called.Should().BeTrue();
    }

    /// <summary>An empty companyId is the action's own "companyId is required" validation to
    /// reject — not a membership question.</summary>
    [Fact]
    public async Task Lets_an_empty_companyId_through_for_the_action_to_reject()
    {
        var context = BuildContext(nameof(FakeController.Scoped), new Dictionary<string, object?> { ["companyId"] = Guid.Empty });
        var called = false;

        await CreateFilter().OnActionExecutionAsync(context, Next(() => called = true));

        called.Should().BeTrue();
    }

    /// <summary>Covers a create request whose companyId travels as a body property instead of a
    /// "companyId" parameter of its own — e.g. CreateBrandRequest.CompanyId.</summary>
    [Fact]
    public async Task Finds_a_companyId_carried_on_a_request_body_property()
    {
        _userCompanyService.CanAccessCompanyAsync(UserId, CompanyId, Arg.Any<CancellationToken>()).Returns(false);

        var context = BuildContext(
            nameof(FakeController.Scoped),
            new Dictionary<string, object?> { ["request"] = new FakeCreateRequest(CompanyId, "Contoso") });

        await CreateFilter().OnActionExecutionAsync(context, NextNotCalled());

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    /// <summary>[AllowAnyCompany] opts an action out entirely — a caller checking its own access
    /// has to get a real answer even when it has none, not a 403.</summary>
    [Fact]
    public async Task Skips_an_action_marked_AllowAnyCompany()
    {
        _userCompanyService.CanAccessCompanyAsync(UserId, CompanyId, Arg.Any<CancellationToken>()).Returns(false);

        var context = BuildContext(nameof(FakeController.Exempt), new Dictionary<string, object?> { ["companyId"] = CompanyId });
        var called = false;

        await CreateFilter().OnActionExecutionAsync(context, Next(() => called = true));

        called.Should().BeTrue();
        await _userCompanyService.DidNotReceive().CanAccessCompanyAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>No subject claim — a machine token with no user identity, for instance — means
    /// there is nobody to check membership for, so the request is refused rather than guessed at.</summary>
    [Fact]
    public async Task Denies_a_token_with_no_user_identity()
    {
        var context = BuildContext(nameof(FakeController.Scoped), new Dictionary<string, object?> { ["companyId"] = CompanyId }, includeSubjectClaim: false);

        await CreateFilter().OnActionExecutionAsync(context, NextNotCalled());

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static ActionExecutingContext BuildContext(
        string actionName,
        IDictionary<string, object?> actionArguments,
        string role = Constants.Roles.User,
        bool includeSubjectClaim = true)
    {
        var claims = new List<Claim> { new(Constants.Claims.Role, role) };
        if (includeSubjectClaim)
            claims.Add(new Claim(Constants.Claims.Subject, UserId));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", Constants.Claims.Name, Constants.Claims.Role))
        };

        var actionDescriptor = new ControllerActionDescriptor
        {
            MethodInfo = typeof(FakeController).GetMethod(actionName)!,
            ControllerTypeInfo = typeof(FakeController).GetTypeInfo()
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);

        return new ActionExecutingContext(
            actionContext, [], actionArguments, controller: new FakeController());
    }

    private static ActionExecutionDelegate Next(Action onCalled) => () =>
    {
        onCalled();
        return Task.FromResult(new ActionExecutedContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [], new FakeController()));
    };

    private static ActionExecutionDelegate NextNotCalled() => () =>
        throw new InvalidOperationException("The action should not have run: the filter should have short-circuited.");

    private sealed record FakeCreateRequest(Guid CompanyId, string Name);

    /// <summary>A stand-in controller purely so MethodInfo/attribute reflection has something real to inspect.</summary>
    private sealed class FakeController
    {
        public void Scoped()
        {
        }

        [AllowAnyCompany]
        public void Exempt()
        {
        }
    }
}
