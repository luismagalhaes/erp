using System.Reflection;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Security;

/// <summary>
/// Nothing gets written for a company with no active package: this runs once for every write
/// action (POST, PUT, PATCH, DELETE), the same way <see cref="RequireCompanyAccessFilter"/> runs
/// once for every action, and refuses to let the action execute when the companyId it carries has
/// no <see cref="Erp.Core.Domain.CompanySubscription"/> row, or one that has already expired.
/// </summary>
/// <remarks>
/// Reads never trip this — a company that lost its subscription can still see its own data, it
/// just cannot add to it. See <see cref="AllowWithoutSubscriptionAttribute"/> for the endpoints
/// that legitimately have to work regardless (creating the company, self-service sign-up, and
/// managing the subscription itself).
/// </remarks>
public sealed class RequireActiveSubscriptionFilter(ICompanySubscriptionService companySubscriptionService) : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!WriteMethods.Contains(context.HttpContext.Request.Method) || IsExempt(context))
        {
            await next();
            return;
        }

        // A SuperAdmin manages every tenant's subscription in the first place — it cannot be
        // locked out by the very thing it is meant to fix.
        if (context.HttpContext.User.IsInRole(Constants.Roles.SuperAdmin))
        {
            await next();
            return;
        }

        if (FindCompanyId(context.ActionArguments) is not { } companyId || companyId == Guid.Empty)
        {
            await next();
            return;
        }

        var subscription = await companySubscriptionService.GetForCompanyAsync(companyId, context.HttpContext.RequestAborted);

        if (subscription is null || subscription.ExpiresAtUtc < DateTime.UtcNow)
        {
            context.Result = new ObjectResult(new { error = "This company has no active subscription. Subscribe to a plan to keep saving data." })
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            return;
        }

        await next();
    }

    private static bool IsExempt(ActionExecutingContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor descriptor
        && (descriptor.MethodInfo.GetCustomAttribute<AllowWithoutSubscriptionAttribute>() is not null
            || descriptor.ControllerTypeInfo.GetCustomAttribute<AllowWithoutSubscriptionAttribute>() is not null);

    /// <summary>Same lookup <see cref="RequireCompanyAccessFilter"/> uses — see its own doc comment.</summary>
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
