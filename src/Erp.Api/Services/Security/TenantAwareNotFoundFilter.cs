using System.Reflection;
using Erp.Common;
using Erp.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Security;

/// <summary>
/// A <c>GET /api/brands/{id}</c> for a brand that belongs to another company already comes back
/// 404 today, because <c>AppDbContext</c>'s tenant filter makes the row invisible before the
/// controller ever sees it — the data is not leaked. What is missing is telling that 404 apart from
/// a truly missing row: <c>RequireCompanyAccessFilter</c> cannot help here, since these actions
/// carry only the resource's own id, never a companyId to check up front. This runs after the
/// action, and only for a controller marked <see cref="ScopedEntityAttribute"/>: if the action
/// answered 404, it asks <see cref="AppDbContext.ExistsForAnotherCompanyAsync"/> — the one place
/// allowed to look past the tenant filter, and only to answer yes or no — and turns the 404 into a
/// 403 when the row does exist, just not for this caller.
/// </summary>
public sealed class TenantAwareNotFoundFilter(ITenantExistenceChecker existenceChecker) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executedContext = await next();

        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        if (descriptor.MethodInfo.GetCustomAttribute<AllowAnyCompanyAttribute>() is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<AllowAnyCompanyAttribute>() is not null)
        {
            return;
        }

        if (descriptor.ControllerTypeInfo.GetCustomAttribute<ScopedEntityAttribute>() is not { } scopedEntity)
            return;

        if (!IsNotFound(executedContext.Result))
            return;

        // A SuperAdmin's context is unrestricted, so a 404 it received already meant "does not
        // exist" — nothing to look past for one.
        if (context.HttpContext.User.IsInRole(Constants.Roles.SuperAdmin))
            return;

        if (FindId(context.ActionArguments) is not { } id)
            return;

        if (await existenceChecker.ExistsForAnotherCompanyAsync(scopedEntity.EntityType, id, context.HttpContext.RequestAborted))
        {
            executedContext.Result = new ObjectResult(new { error = "You do not have access to this resource." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }

    private static bool IsNotFound(IActionResult? result) => result switch
    {
        NotFoundResult or NotFoundObjectResult => true,
        ObjectResult objectResult => objectResult.StatusCode == StatusCodes.Status404NotFound,
        _ => false
    };

    private static Guid? FindId(IDictionary<string, object?> actionArguments) =>
        actionArguments.TryGetValue("id", out var value) && value is Guid guid && guid != Guid.Empty ? guid : null;
}
