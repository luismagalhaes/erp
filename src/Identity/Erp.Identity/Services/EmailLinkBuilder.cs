using Microsoft.AspNetCore.WebUtilities;

namespace Erp.Identity.Services;

/// <summary>
/// Builds the absolute links that go into an email — account confirmation, password reset — from
/// the host's own configured address, never from <c>NavigationManager.BaseUri</c>.
/// </summary>
/// <remarks>
/// A link inside an email is opened outside the browser session that generated it — a different
/// tab, a different device, hours later — so it cannot rely on whatever the current request
/// happened to look like. <c>IdentityServer:Authority</c> is the one setting already guaranteed to
/// be this host's own real, external address: Duende itself issues tokens under it, so it can never
/// silently drift the way inferring a host from the request can.
/// </remarks>
public static class EmailLinkBuilder
{
    public static string Build(IConfiguration configuration, string path, IDictionary<string, string?> query)
    {
        var authority = configuration["IdentityServer:Authority"]?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(authority))
            throw new InvalidOperationException("IdentityServer:Authority is not configured.");

        // Left out entirely rather than sent as "key=" — an absent returnUrl should not survive as
        // an empty one.
        var nonEmpty = query
            .Where(pair => !string.IsNullOrEmpty(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        return authority + QueryHelpers.AddQueryString(path, nonEmpty);
    }
}
