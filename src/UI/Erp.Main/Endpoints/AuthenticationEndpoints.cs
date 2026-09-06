using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Erp.Main.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/authentication/login", async (HttpContext httpContext, string? returnUrl) =>
        {
            if (httpContext.User.Identity?.IsAuthenticated is true)
            {
                httpContext.Response.Redirect(SanitizeReturnUrl(returnUrl));
                return;
            }

            await httpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties
            {
                RedirectUri = SanitizeReturnUrl(returnUrl),
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            });
        }).AllowAnonymous();

        app.MapGet("/authentication/logout", (string? returnUrl) =>
        {
            var target = SanitizeReturnUrl(returnUrl);

            return Results.SignOut(
                new AuthenticationProperties
                {
                    RedirectUri = $"/authentication/post-logout?returnUrl={Uri.EscapeDataString(target)}"
                },
                [
                    OpenIdConnectDefaults.AuthenticationScheme,
                    CookieAuthenticationDefaults.AuthenticationScheme
                ]);
        }).RequireAuthorization();

        app.MapGet("/authentication/post-logout", (HttpContext httpContext, string? returnUrl) =>
        {
            httpContext.Response.Redirect(SanitizeReturnUrl(returnUrl));
            return Task.CompletedTask;
        }).AllowAnonymous();
    }

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Relative, out var relativeUri) &&
            returnUrl.StartsWith('/'))
        {
            return relativeUri.ToString();
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.PathAndQuery;
        }

        return "/";
    }
}
