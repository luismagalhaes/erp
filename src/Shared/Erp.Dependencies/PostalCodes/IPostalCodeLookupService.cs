namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// Looks up the address behind a Portuguese postal code, so a form can fill locality/municipality
/// from the "0000-000" the user typed instead of asking for them separately.
/// </summary>
public interface IPostalCodeLookupService
{
    /// <summary>
    /// Returns the address for <paramref name="postalCode"/>, or null when the code is not a valid
    /// "0000-000" shape or moradas.dev has no match for it.
    /// </summary>
    Task<PostalCodeLookupResult?> LookupAsync(string postalCode, CancellationToken cancellationToken = default);
}
