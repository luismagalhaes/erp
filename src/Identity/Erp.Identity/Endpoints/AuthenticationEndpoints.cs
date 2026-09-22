using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Erp.Identity.Data;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        // app.Logger — rather than an injected ILogger<T> — is what the lambdas below use: this
        // static class can never be a generic type argument, and a plain WebApplication.Logger
        // already carries the right category without one.
        var logger = app.Logger;

        app.MapGet("/authentication/logout", async (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, HttpContext httpContext, string? returnUrl, string? logoutId) =>
        {
            logger.LogInformation(
                "{Class}.{Method} called with logoutId={LogoutId}", nameof(AuthenticationEndpoints), "Logout", logoutId);

            var target = ReturnUrlHelper.Sanitize(returnUrl);

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

        app.MapPost("/authentication/login", async (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, ILoginAuditService loginAudit, HttpContext httpContext) =>
        {
            // Email and password never reach the log — only the outcome and, on success, the
            // signed-in user's id. The persisted LoginAudit row is the one place the attempted
            // email is kept, for the backoffice's own Session Logs page — never the password.
            logger.LogInformation("{Class}.{Method} called", nameof(AuthenticationEndpoints), "Login");

            var form = await httpContext.Request.ReadFormAsync();
            var email = (form["email"].ToString() ?? string.Empty).Trim();
            var password = form["password"].ToString() ?? string.Empty;
            var returnUrl = ReturnUrlHelper.Sanitize(form["returnUrl"].ToString());
            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                httpContext.Response.Redirect($"/Account/SignIn?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
                return;
            }

            var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await signInManager.UserManager.FindByEmailAsync(email);
                logger.LogInformation(
                    "{Class}.{Method} succeeded for userId={UserId}, remoteIp={RemoteIp}",
                    nameof(AuthenticationEndpoints), "Login", user?.Id, remoteIp);

                await loginAudit.RecordAsync(user?.Id, email, succeeded: true, failureReason: null, remoteIp);

                if (interaction.IsValidReturnUrl(returnUrl))
                {
                    httpContext.Response.Redirect(returnUrl);
                }
                else
                {
                    httpContext.Response.Redirect(ReturnUrlHelper.Sanitize(returnUrl));
                }

                return;
            }

            logger.LogWarning(
                "{Class}.{Method} failed, lockedOut={LockedOut}, notAllowed={NotAllowed}, remoteIp={RemoteIp}",
                nameof(AuthenticationEndpoints), "Login", result.IsLockedOut, result.IsNotAllowed, remoteIp);

            // NotAllowed is what SignInManager returns when SignIn.RequireConfirmedAccount is on
            // and the account's email is still unconfirmed — the one other reason this can fail,
            // since nothing else in this app's options makes an account "not allowed" to sign in.
            string errorType;
            string failureReason;

            if (result.IsLockedOut)
            {
                errorType = "locked";
                failureReason = "LockedOut";
            }
            else if (result.IsNotAllowed)
            {
                errorType = "unconfirmed";
                failureReason = "EmailNotConfirmed";
            }
            else
            {
                errorType = "1";
                failureReason = "InvalidCredentials";
            }

            var failedUser = await signInManager.UserManager.FindByEmailAsync(email);
            await loginAudit.RecordAsync(failedUser?.Id, email, succeeded: false, failureReason, remoteIp);

            var emailParameter = errorType == "unconfirmed" ? $"&email={Uri.EscapeDataString(email)}" : string.Empty;
            httpContext.Response.Redirect($"/Account/SignIn?error={errorType}&returnUrl={Uri.EscapeDataString(returnUrl)}{emailParameter}");
        }).AllowAnonymous();

        // A Blazor Server component can never write the auth cookie itself — the circuit is a
        // long-lived SignalR connection, not a fresh request/response, so by the time a component
        // runs the response has already started. Changing the password or the email bumps the
        // security stamp the cookie is checked against, so after either the Profile page force-loads
        // this plain endpoint, which runs in a normal request and can reissue the cookie.
        app.MapGet("/authentication/refresh-sign-in", async (SignInManager<ApplicationUser> signInManager, HttpContext httpContext, string? returnUrl) =>
        {
            logger.LogInformation("{Class}.{Method} called", nameof(AuthenticationEndpoints), "RefreshSignIn");

            var user = await signInManager.UserManager.GetUserAsync(httpContext.User);
            if (user is not null)
                await signInManager.RefreshSignInAsync(user);

            httpContext.Response.Redirect(ReturnUrlHelper.Sanitize(returnUrl));
        }).RequireAuthorization();
    }
}
