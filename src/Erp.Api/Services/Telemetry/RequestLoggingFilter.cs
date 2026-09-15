using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Telemetry;

/// <summary>
/// Logs every controller action that runs — class, method and the simple identifiers the caller
/// passed (companyId, id, ...) — and any exception the action lets propagate, one line each.
/// Registered once as a global MVC filter instead of every action logging its own entry.
/// </summary>
public sealed class RequestLoggingFilter(ILogger<RequestLoggingFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controllerName = context.Controller.GetType().Name;
        var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
            ? action
            : context.ActionDescriptor.DisplayName;

        var parameters = string.Join(", ", context.ActionArguments
            .Where(argument => IsLoggable(argument.Value))
            .Select(argument => $"{argument.Key}={argument.Value}"));

        logger.LogInformation("{Controller}.{Action} called ({Parameters})", controllerName, actionName, parameters);

        var executedContext = await next();

        if (executedContext.Exception is not null && !executedContext.ExceptionHandled)
        {
            logger.LogError(
                executedContext.Exception, "{Controller}.{Action} failed.", controllerName, actionName);
        }
    }

    /// <summary>
    /// Only simple identifiers are logged — request bodies and cancellation tokens are excluded so
    /// the entry log stays one line and never leaks a full payload.
    /// </summary>
    private static bool IsLoggable(object? value) => value switch
    {
        null => false,
        string or Guid or DateTime or DateOnly or Enum => true,
        _ => value.GetType().IsPrimitive
    };
}
