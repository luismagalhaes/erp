using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Erp.Identity.Common.Constants;
using Erp.Identity.Data;
using Erp.Identity.Infrastructure.Application;
using Erp.Identity.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Endpoints;

public static class AuthenticationEndpoints
{
    // Microsoft Graph's "mail" field (the default source for ClaimTypes.Email) is null for a lot
    // of real accounts; Program.cs maps "userPrincipalName" into this claim as a fallback, read
    // in HandleExternalLoginCallbackAsync below. Public because Program.cs is where the ClaimAction
    // is registered, and the two need to agree on the exact same claim type string.
    public const string MicrosoftUserPrincipalNameClaimType = "urn:microsoft:userprincipalname";

    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        // app.Logger — rather than an injected ILogger<T> — is what every handler below closes
        // over: this static class can never be a generic type argument, and a plain
        // WebApplication.Logger already carries the right category without one. Each route's own
        // logic lives in a named method instead of an inline lambda, so this method stays a plain
        // list of routes and every handler can be read and tested on its own.
        var logger = app.Logger;

        app.MapGet("/authentication/logout", (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, HttpContext httpContext, string? returnUrl, string? logoutId) =>
            LogoutAsync(signInManager, interaction, httpContext, logger, returnUrl, logoutId))
            .RequireAuthorization();

        app.MapPost("/authentication/login", (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, ILoginAuditService loginAudit, IRecaptchaVerifier recaptcha, HttpContext httpContext) =>
            LoginAsync(signInManager, interaction, loginAudit, recaptcha, httpContext, logger))
            .AllowAnonymous();

        app.MapGet("/authentication/google-login", (SignInManager<ApplicationUser> signInManager, string? returnUrl) =>
            ChallengeExternalLogin(signInManager, GoogleDefaults.AuthenticationScheme, "/authentication/google-callback", returnUrl))
            .AllowAnonymous();

        app.MapGet("/authentication/google-callback", (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, ILoginAuditService loginAudit, HttpContext httpContext, string? returnUrl) =>
            HandleExternalLoginCallbackAsync("Google", signInManager, interaction, loginAudit, httpContext, logger, returnUrl))
            .AllowAnonymous();

        app.MapGet("/authentication/microsoft-login", (SignInManager<ApplicationUser> signInManager, string? returnUrl) =>
            ChallengeExternalLogin(signInManager, MicrosoftAccountDefaults.AuthenticationScheme, "/authentication/microsoft-callback", returnUrl))
            .AllowAnonymous();

        app.MapGet("/authentication/microsoft-callback", (SignInManager<ApplicationUser> signInManager, IIdentityServerInteractionService interaction, ILoginAuditService loginAudit, HttpContext httpContext, string? returnUrl) =>
            HandleExternalLoginCallbackAsync("Microsoft", signInManager, interaction, loginAudit, httpContext, logger, returnUrl))
            .AllowAnonymous();

        // A Blazor Server component can never write the auth cookie itself — the circuit is a
        // long-lived SignalR connection, not a fresh request/response, so by the time a component
        // runs the response has already started. Changing the password or the email bumps the
        // security stamp the cookie is checked against, so after either the Profile page force-loads
        // this plain endpoint, which runs in a normal request and can reissue the cookie.
        app.MapGet("/authentication/refresh-sign-in", (SignInManager<ApplicationUser> signInManager, HttpContext httpContext, string? returnUrl) =>
            RefreshSignInAsync(signInManager, httpContext, logger, returnUrl))
            .RequireAuthorization();
    }

    private static async Task LogoutAsync(
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        HttpContext httpContext,
        ILogger logger,
        string? returnUrl,
        string? logoutId)
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
    }

    private static async Task LoginAsync(
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        ILoginAuditService loginAudit,
        IRecaptchaVerifier recaptcha,
        HttpContext httpContext,
        ILogger logger)
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

        // Re-decided here, not trusted from the client: SignIn.razor only renders the widget past
        // this same threshold, but the actual gate is this check, run again against the current
        // count right before the credentials are even looked at.
        if (recaptcha.IsConfigured)
        {
            var recentFailures = await loginAudit.CountRecentFailuresAsync(remoteIp ?? "unknown", Constants.Recaptcha.Window);
            if (recentFailures >= Constants.Recaptcha.AttemptThreshold)
            {
                var captchaToken = form["g-recaptcha-response"].ToString();
                var captchaOk = await recaptcha.VerifyAsync(captchaToken, remoteIp, Constants.Recaptcha.SignInAction);
                if (!captchaOk)
                {
                    logger.LogWarning("{Class}.{Method} failed reCAPTCHA, remoteIp={RemoteIp}", nameof(AuthenticationEndpoints), "Login", remoteIp);
                    await loginAudit.RecordAsync(null, email, succeeded: false, "RecaptchaFailed", remoteIp);
                    httpContext.Response.Redirect($"/Account/SignIn?error=captcha&returnUrl={Uri.EscapeDataString(returnUrl)}");
                    return;
                }
            }
        }

        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: true, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            var user = await signInManager.UserManager.FindByEmailAsync(email);
            logger.LogInformation(
                "{Class}.{Method} succeeded for userId={UserId}, remoteIp={RemoteIp}",
                nameof(AuthenticationEndpoints), "Login", user?.Id, remoteIp);

            await loginAudit.RecordAsync(user?.Id, email, succeeded: true, failureReason: null, remoteIp);

            httpContext.Response.Redirect(interaction.IsValidReturnUrl(returnUrl) ? returnUrl : ReturnUrlHelper.Sanitize(returnUrl));
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
    }

    private static async Task RefreshSignInAsync(
        SignInManager<ApplicationUser> signInManager,
        HttpContext httpContext,
        ILogger logger,
        string? returnUrl)
    {
        logger.LogInformation("{Class}.{Method} called", nameof(AuthenticationEndpoints), "RefreshSignIn");

        var user = await signInManager.UserManager.GetUserAsync(httpContext.User);
        if (user is not null)
            await signInManager.RefreshSignInAsync(user);

        httpContext.Response.Redirect(ReturnUrlHelper.Sanitize(returnUrl));
    }

    private static IResult ChallengeExternalLogin(
        SignInManager<ApplicationUser> signInManager, string scheme, string callbackPath, string? returnUrl)
    {
        var target = ReturnUrlHelper.Sanitize(returnUrl);
        var redirectUrl = $"{callbackPath}?returnUrl={Uri.EscapeDataString(target)}";

        // A bare `new AuthenticationProperties { RedirectUri = ... }` is missing the "LoginProvider"
        // item this helper adds — without it the external cookie still authenticates fine after the
        // provider redirects back, but SignInManager.GetExternalLoginInfoAsync always returns null
        // anyway, since it specifically checks for that item.
        var properties = signInManager.ConfigureExternalAuthenticationProperties(scheme, redirectUrl);

        return Results.Challenge(properties, [scheme]);
    }

    // Shared by every external provider's callback (Google, Microsoft, ...): first tries the
    // already-linked AspNetUserLogins row, then falls back to matching/creating a user by email.
    // providerName is only used for log lines and the LoginAudit failureReason, e.g. "Google" ->
    // "GoogleNoEmail" — the actual provider is whatever SignInManager.GetExternalLoginInfoAsync
    // picked up from the external cookie set by the challenge.
    private static async Task HandleExternalLoginCallbackAsync(
        string providerName,
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        ILoginAuditService loginAudit,
        HttpContext httpContext,
        ILogger logger,
        string? returnUrl)
    {
        var methodName = $"{providerName}Callback";
        logger.LogInformation("{Class}.{Method} called", nameof(AuthenticationEndpoints), methodName);

        var target = ReturnUrlHelper.Sanitize(returnUrl);
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            // GetExternalLoginInfoAsync swallows why — re-running the same authenticate call it
            // does internally, only to log the result, is the only way to see whether the external
            // cookie never arrived at all versus arrived but was missing the provider/key items it
            // needs.
            var externalAuth = await httpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
            logger.LogWarning(
                "{Class}.{Method} had no external login info, remoteIp={RemoteIp}, authSucceeded={AuthSucceeded}, failure={Failure}, hasPrincipal={HasPrincipal}",
                nameof(AuthenticationEndpoints), methodName, remoteIp, externalAuth.Succeeded, externalAuth.Failure?.Message, externalAuth.Principal is not null);
            httpContext.Response.Redirect($"/Account/SignIn?error=external&returnUrl={Uri.EscapeDataString(target)}");
            return;
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
            ?? info.Principal.FindFirstValue(MicrosoftUserPrincipalNameClaimType);
        var userManager = signInManager.UserManager;

        // Already-linked account: this is the common case from the second sign-in onward.
        var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        ApplicationUser? user;
        if (result.Succeeded)
        {
            user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        }
        else if (result.IsLockedOut)
        {
            await loginAudit.RecordAsync(null, email ?? "unknown", succeeded: false, "LockedOut", remoteIp);
            httpContext.Response.Redirect($"/Account/SignIn?error=locked&returnUrl={Uri.EscapeDataString(target)}");
            return;
        }
        else if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("{Class}.{Method} got no email claim from {Provider}, remoteIp={RemoteIp}", nameof(AuthenticationEndpoints), methodName, providerName, remoteIp);
            await loginAudit.RecordAsync(null, "unknown", succeeded: false, $"{providerName}NoEmail", remoteIp);
            httpContext.Response.Redirect($"/Account/SignIn?error=external&returnUrl={Uri.EscapeDataString(target)}");
            return;
        }
        else
        {
            // First time this external account signs in. Same open self-registration model as
            // SignUp.razor — the only difference is the provider already verified the email, so
            // the new account starts out confirmed. If the email matches an existing password
            // account instead, this just links the external login to it as an extra way to sign in.
            user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
                    LastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    logger.LogWarning(
                        "{Class}.{Method} could not create user for email={Email}: {Errors}",
                        nameof(AuthenticationEndpoints), methodName, email, string.Join(" ", createResult.Errors.Select(e => e.Description)));
                    await loginAudit.RecordAsync(null, email, succeeded: false, $"{providerName}ProvisionFailed", remoteIp);
                    httpContext.Response.Redirect($"/Account/SignIn?error=external&returnUrl={Uri.EscapeDataString(target)}");
                    return;
                }
            }

            var addLoginResult = await userManager.AddLoginAsync(user, info);
            if (!addLoginResult.Succeeded)
            {
                logger.LogWarning(
                    "{Class}.{Method} could not link {Provider} login for userId={UserId}: {Errors}",
                    nameof(AuthenticationEndpoints), methodName, providerName, user.Id, string.Join(" ", addLoginResult.Errors.Select(e => e.Description)));
                await loginAudit.RecordAsync(user.Id, email, succeeded: false, $"{providerName}LinkFailed", remoteIp);
                httpContext.Response.Redirect($"/Account/SignIn?error=external&returnUrl={Uri.EscapeDataString(target)}");
                return;
            }

            await signInManager.SignInAsync(user, isPersistent: true, info.LoginProvider);
        }

        await httpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        logger.LogInformation(
            "{Class}.{Method} succeeded for userId={UserId}, remoteIp={RemoteIp}",
            nameof(AuthenticationEndpoints), methodName, user?.Id, remoteIp);
        await loginAudit.RecordAsync(user?.Id, email ?? user?.Email ?? "unknown", succeeded: true, failureReason: null, remoteIp);

        httpContext.Response.Redirect(interaction.IsValidReturnUrl(target) ? target : ReturnUrlHelper.Sanitize(target));
    }
}
