using System.Reflection;
using System.Security.Claims;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Security;

/// <summary>
/// Every business-data endpoint is scoped to a company, taken from the caller as a plain
/// "companyId" — a query parameter, a route value, or a property on the request body — and, until
/// this filter, never checked against who the caller actually is. A token only proves who signed
/// in, not which companies they belong to: a caller with a valid Read or Write scope could ask for
/// any tenant's data simply by changing that one value. This runs once for every action instead of
/// each controller re-checking on its own — see <see cref="AllowAnyCompanyAttribute"/> for the
/// handful of endpoints that legitimately opt out.
/// </summary>
/// <remarks>
/// Deliberately app-boundary enforcement, not a database-level safeguard: every storage query
/// already filters correctly by whichever companyId it is given (see the module storages under
/// Infrastructure/Storage) — the gap was never "the wrong rows come back", it was "the caller was
/// never asked whether that companyId was theirs to ask for" in the first place. Closing it here,
/// before a single query runs, is both the correct layer and the only one that has the caller's
/// identity to check against.
/// </remarks>
public sealed class RequireCompanyAccessFilter(IUserCompanyService userCompanyService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (IsExempt(context))
        {
            await next();
            return;
        }

        // No companyId in play (most master-data lookups, e.g. VAT rates), or an empty one the
        // action's own validation already rejects: nothing here for this filter to police.
        if (FindCompanyId(context.ActionArguments) is not { } companyId || companyId == Guid.Empty)
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;

        // A SuperAdmin manages every tenant already — the same reach CompaniesController grants
        // under Policies.Admin — so the membership table has nothing further to say here.
        if (user.IsInRole(Constants.Roles.SuperAdmin))
        {
            await next();
            return;
        }

        var userId = user.FindFirstValue(Constants.Claims.Subject) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId)
            || !await userCompanyService.CanAccessCompanyAsync(userId, companyId, context.HttpContext.RequestAborted))
        {
            context.Result = new ObjectResult(new { error = "You do not have access to this company." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static bool IsExempt(ActionExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor descriptor
        && (descriptor.MethodInfo.GetCustomAttribute<AllowAnyCompanyAttribute>() is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<AllowAnyCompanyAttribute>() is not null);

    /// <summary>
    /// A "companyId" argument — a query string, route value or body property model-bound to that
    /// exact parameter name — or, failing that, a "CompanyId" property on whichever request body
    /// the action took. Covers every shape used across the API: <c>[FromQuery] Guid companyId</c>,
    /// <c>{companyId:guid}</c> route segments, and a create request's own <c>CompanyId</c>.
    /// </summary>
    private static Guid? FindCompanyId(IDictionary<string, object?> actionArguments)
    {
        if (actionArguments.TryGetValue("companyId", out var direct) && direct is Guid directGuid && directGuid != Guid.Empty)
            return directGuid;

        foreach (var value in actionArguments.Values)
        {
            if (value is null or string || value.GetType().IsPrimitive)
                continue;

            if (value.GetType().GetProperty("CompanyId")?.GetValue(value) is Guid propertyValue && propertyValue != Guid.Empty)
                return propertyValue;
        }

        return null;
    }
}
