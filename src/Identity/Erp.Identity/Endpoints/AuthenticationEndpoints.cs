using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Erp.Identity.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/authentication/logout", async (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, HttpContext httpContext, string? returnUrl, string? logoutId) =>
        {
            var target = SanitizeReturnUrl(returnUrl);

            await signInManager.SignOutAsync();
            await httpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            await httpContext.SignOutAsync(IdentityServerConstants.DefaultCookieAuthenticationScheme);

            if (!string.IsNullOrWhiteSpace(logoutId))
            {
                var logoutContext = await interaction.GetLogoutContextAsync(logoutId);
                if (!string.IsNullOrWhiteSpace(logoutContext?.PostLogoutRedirectUri))
                {
                    target = logoutContext.PostLogoutRedirectUri;
                }
            }

            httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            httpContext.Response.Headers.Pragma = "no-cache";
            httpContext.Response.Redirect(target);
        }).RequireAuthorization();

        app.MapPost("/authentication/login", async (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, HttpContext httpContext) =>
        {
            var form = await httpContext.Request.ReadFormAsync();
            var email = (form["email"].ToString() ?? string.Empty).Trim();
            var password = form["password"].ToString() ?? string.Empty;
            var returnUrl = SanitizeReturnUrl(form["returnUrl"].ToString());

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                httpContext.Response.Redirect($"/Account/SignIn?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }

            var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                if (interaction.IsValidReturnUrl(returnUrl))
                {
                    httpContext.Response.Redirect(returnUrl);
                }
                else
                {
                    httpContext.Response.Redirect(SanitizeReturnUrl(returnUrl));
                }

                return;
            }

            var errorType = result.IsLockedOut ? "locked" : "1";
            httpContext.Response.Redirect($"/Account/SignIn?error={errorType}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }).AllowAnonymous();
    }

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        return returnUrl;
    }
}
