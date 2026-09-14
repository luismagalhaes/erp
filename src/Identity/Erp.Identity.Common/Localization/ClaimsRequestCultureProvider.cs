using IdentityConstants = Erp.Identity.Common.Constants.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Erp.Identity.Common.Localization;

/// <summary>
/// Reads the culture from the "locale" claim (<see cref="IdentityConstants.Claims.Locale"/>) of the
/// signed-in user, so an authenticated visitor always gets the language stored on their
/// Identity profile instead of relying on a cookie or the browser's Accept-Language header.
/// Register it before <see cref="CookieRequestCultureProvider"/> so it wins whenever the user is
/// authenticated, falling back to the cookie/header providers for anonymous visitors.
/// </summary>
public sealed class ClaimsRequestCultureProvider : IRequestCultureProvider
{
    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var locale = httpContext.User.FindFirst(IdentityConstants.Claims.Locale)?.Value;

        return Task.FromResult(string.IsNullOrWhiteSpace(locale)
            ? null
            : new ProviderCultureResult(locale));
    }
}
