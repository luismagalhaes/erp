using System.Security.Claims;
using Duende.IdentityServer.AspNetIdentity;
using Duende.IdentityServer.Models;
using Erp.Common;
using Erp.Identity.Data;
using Microsoft.AspNetCore.Identity;

namespace Erp.Identity.Storage.Services;

/// <summary>
/// Extends the default ASP.NET Identity profile service to also issue the "locale" claim, so
/// every ERP UI can read the user's preferred language straight from the id_token/access token
/// without an extra call to the Identity host. "locale" is part of the standard OIDC "profile"
/// scope, so no extra identity resource is required.
/// </summary>
public sealed class LocalizedProfileService(
    UserManager<ApplicationUser> userManager,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory)
    : ProfileService<ApplicationUser>(userManager, claimsFactory)
{
    protected override async Task GetProfileDataAsync(ProfileDataRequestContext context, ApplicationUser user)
    {
        await base.GetProfileDataAsync(context, user);

        if (context.RequestedClaimTypes.Contains(Constants.Claims.Locale))
        {
            var preferredLanguage = string.IsNullOrWhiteSpace(user.PreferredLanguage)
                ? Constants.Localization.DefaultCulture
                : user.PreferredLanguage;

            context.IssuedClaims.RemoveAll(x => x.Type == Constants.Claims.Locale);
            context.IssuedClaims.Add(new Claim(Constants.Claims.Locale, preferredLanguage));
        }
    }
}
