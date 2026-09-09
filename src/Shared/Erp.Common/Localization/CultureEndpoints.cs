using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Erp.Common.Localization;

/// <summary>
/// Minimal endpoint used by the language switcher in the header of every ERP Blazor app. It
/// persists the chosen culture in the culture cookie (used for anonymous visitors, and as a
/// fallback until the "locale" claim catches up after a profile update) and redirects back to
/// the page the user was on, forcing a full reload so the new culture takes effect.
/// </summary>
public static class CultureEndpoints
{
    public static void MapCultureEndpoints(this WebApplication app)
    {
        app.MapGet("/culture/set", (HttpContext httpContext, string culture, string? redirectUri) =>
        {
            if (!Constants.Localization.SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
                culture = Constants.Localization.DefaultCulture;

            httpContext.Response.Cookies.Append(
                Constants.Localization.CultureCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

            var target = string.IsNullOrWhiteSpace(redirectUri) || !redirectUri.StartsWith('/')
                ? "/"
                : redirectUri;

            httpContext.Response.Redirect(target);
        }).AllowAnonymous();
    }
}
