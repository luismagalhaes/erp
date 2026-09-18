namespace Erp.Dependencies.PostalCodes;

/// <summary>
/// The address details geoapi.pt returns for a Portuguese postal code (e.g. "4805-476").
/// </summary>
public sealed record PostalCodeLookupResult(
    string PostalCode,
    string? Locality,
    string? Municipality,
    string? District,
    string? Parish);
