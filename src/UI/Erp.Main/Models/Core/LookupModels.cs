namespace Erp.Main.Models.Core;

/// <summary>Mirrors Erp.Dependencies' PostalCodeLookupResult, the API's JSON shape for a lookup.</summary>
public sealed record PostalCodeLookupResult(
    string PostalCode,
    string? Locality,
    string? Municipality,
    string? District,
    string? Parish,
    IReadOnlyList<PostalCodeStreet> Streets);

/// <summary>Mirrors Erp.Dependencies' PostalCodeStreet.</summary>
public sealed record PostalCodeStreet(string Name, string? Segment);

/// <summary>Mirrors Erp.Dependencies' VatNumberValidationSource.</summary>
public enum VatNumberValidationSource
{
    LocalOnly,
    Vies
}

/// <summary>Mirrors Erp.Dependencies' VatNumberValidationResult, the API's JSON shape for a lookup.</summary>
public sealed record VatNumberValidationResult(
    bool IsStructurallyValid,
    VatNumberValidationSource Source,
    bool? IsRegisteredInVies,
    string? RegisteredName,
    string? RegisteredAddress,
    string? RegisteredPostalCode = null,
    string? RegisteredCity = null);
