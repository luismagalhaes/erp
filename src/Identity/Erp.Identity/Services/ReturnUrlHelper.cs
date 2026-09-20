namespace Erp.Identity.Services;

/// <summary>
/// What every page along the sign-up/sign-in/confirm-email chain uses to keep a "returnUrl" safe
/// to redirect to. It only has to look right here — <c>IIdentityServerInteractionService.IsValidReturnUrl</c>
/// is the real gate, checked again in <c>AuthenticationEndpoints</c> right before the redirect that
/// actually completes an OIDC flow.
/// </summary>
public static class ReturnUrlHelper
{
    /// <summary>A relative path, or an absolute http(s) URL — anything else falls back to "/".</summary>
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if (returnUrl.StartsWith('/') && Uri.TryCreate(returnUrl, UriKind.Relative, out var relative))
            return relative.ToString();

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        return "/";
    }
}
