namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// The address details moradas.dev returns for a Portuguese postal code (e.g. "4805-476").
/// </summary>
/// <param name="Parish">
/// Always null — moradas.dev has no equivalent of the freguesia (parish) the previous provider,
/// geoapi.pt, used to return. Kept so callers that already handle a missing parish need no change.
/// </param>
/// <param name="Streets">
/// The streets moradas.dev lists for this postal code, most specific codes down to a handful and
/// some to just one. Empty, never null, when moradas.dev lists none.
/// </param>
public sealed record PostalCodeLookupResult(
    string PostalCode,
    string? Locality,
    string? Municipality,
    string? District,
    string? Parish,
    IReadOnlyList<PostalCodeStreet> Streets);

/// <summary>
/// One street moradas.dev lists for a postal code. <paramref name="Segment"/> is the portion of the
/// street this postal code covers (e.g. "Impares de 1 a 19" — odd numbers 1 to 19), when the street
/// is split across more than one postal code; null when it covers the whole street.
/// </summary>
public sealed record PostalCodeStreet(string Name, string? Segment);
