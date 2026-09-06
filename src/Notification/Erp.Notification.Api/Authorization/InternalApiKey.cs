using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Notification.Api.Authorization;

/// <summary>
/// Guards the queue endpoint, which is called service to service by the Identity host and
/// therefore carries no user token. Without it the endpoint would be an open mail relay for
/// anyone who can reach the service.
/// </summary>
public sealed class InternalApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Api-Key";
    public const string ConfigurationKey = "Notification:InternalApiKey";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();

        var expected = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(expected))
        {
            // Development keeps working without configuration, but the gap is logged loudly.
            if (environment.IsDevelopment())
            {
                services.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<InternalApiKeyAttribute>()
                    .LogWarning(
                        "{Key} is not configured; the queue endpoint is unprotected. Set it before any shared environment.",
                        ConfigurationKey);

                await next();
                return;
            }

            context.Result = new ObjectResult(new { error = "The service is not configured to accept internal calls." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!string.Equals(provided, expected, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid internal API key." });
            return;
        }

        await next();
    }
}
