using System.Reflection;
using System.Security.Claims;
using Erp.Api.Security;
using Erp.Common;
using Erp.Storage;
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
/// A brand that belongs to another company already comes back 404 — the tenant filter hid it
/// before the controller ever saw it, so no data leaks either way. This filter only decides which
/// kind of 404 the caller gets, or whether it becomes a 403 instead: these tests pin that decision
/// down without needing a real database, since the only real-database risk here (the reflection in
/// AppDbContext.ExistsForAnotherCompanyAsync) is proven separately, against LocalDB, in
/// Erp.IntegrationTests.
/// </summary>
public class TenantAwareNotFoundFilterTests
{
    private readonly ITenantExistenceChecker _existenceChecker = Substitute.For<ITenantExistenceChecker>();
    private static readonly Guid BrandId = Guid.NewGuid();

    private TenantAwareNotFoundFilter CreateFilter() => new(_existenceChecker);

    [Fact]
    public async Task Turns_a_404_into_a_403_when_the_row_exists_for_another_company()
    {
        _existenceChecker.ExistsForAnotherCompanyAsync(typeof(FakeEntity), BrandId, Arg.Any<CancellationToken>()).Returns(true);

        var context = BuildContext(nameof(ScopedController.GetById), typeof(ScopedController), new NotFoundResult());

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Leaves_a_genuine_404_alone()
    {
        _existenceChecker.ExistsForAnotherCompanyAsync(typeof(FakeEntity), BrandId, Arg.Any<CancellationToken>()).Returns(false);

        var context = BuildContext(nameof(ScopedController.GetById), typeof(ScopedController), new NotFoundResult());

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Leaves_a_successful_result_alone()
    {
        var context = BuildContext(nameof(ScopedController.GetById), typeof(ScopedController), new OkObjectResult(new { }));

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<OkObjectResult>();
        await _existenceChecker.DidNotReceive().ExistsForAnotherCompanyAsync(
            Arg.Any<Type>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Skips_a_controller_with_no_ScopedEntity()
    {
        var context = BuildContext(nameof(UnscopedController.GetById), typeof(UnscopedController), new NotFoundResult());

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<NotFoundResult>();
        await _existenceChecker.DidNotReceive().ExistsForAnotherCompanyAsync(
            Arg.Any<Type>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A caller checking its own access has to get a real 404/answer, not a 403 revealing
    /// that some other caller's row exists.</summary>
    [Fact]
    public async Task Skips_an_action_marked_AllowAnyCompany()
    {
        _existenceChecker.ExistsForAnotherCompanyAsync(typeof(FakeEntity), BrandId, Arg.Any<CancellationToken>()).Returns(true);

        var context = BuildContext(nameof(ScopedController.ExemptGetById), typeof(ScopedController), new NotFoundResult());

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Skips_a_SuperAdmin_whose_404_already_meant_the_row_does_not_exist()
    {
        var context = BuildContext(
            nameof(ScopedController.GetById), typeof(ScopedController), new NotFoundResult(), role: Constants.Roles.SuperAdmin);

        await CreateFilter().OnActionExecutionAsync(context.Executing, Next(context.Result));

        context.Result.Result.Should().BeOfType<NotFoundResult>();
        await _existenceChecker.DidNotReceive().ExistsForAnotherCompanyAsync(
            Arg.Any<Type>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static (ActionExecutingContext Executing, ActionExecutedContext Result) BuildContext(
        string actionName,
        Type controllerType,
        IActionResult result,
        string role = Constants.Roles.User)
    {
        var claims = new List<Claim> { new(Constants.Claims.Role, role), new(Constants.Claims.Subject, "user-1") };

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test", Constants.Claims.Name, Constants.Claims.Role))
        };

        var actionDescriptor = new ControllerActionDescriptor
        {
            MethodInfo = controllerType.GetMethod(actionName)!,
            ControllerTypeInfo = controllerType.GetTypeInfo()
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), actionDescriptor);
        var arguments = new Dictionary<string, object?> { ["id"] = BrandId };

        var executing = new ActionExecutingContext(actionContext, [], arguments, controller: Activator.CreateInstance(controllerType)!);
        var executed = new ActionExecutedContext(actionContext, [], executing.Controller) { Result = result };

        return (executing, executed);
    }

    private static ActionExecutionDelegate Next(ActionExecutedContext result) => () => Task.FromResult(result);

    private sealed class FakeEntity;

    [ScopedEntity(typeof(FakeEntity))]
    private sealed class ScopedController
    {
        public void GetById()
        {
        }

        [AllowAnyCompany]
        public void ExemptGetById()
        {
        }
    }

    private sealed class UnscopedController
    {
        public void GetById()
        {
        }
    }
}
