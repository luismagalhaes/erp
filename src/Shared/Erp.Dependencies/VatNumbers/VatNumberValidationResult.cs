namespace Erp.Dependencies.VatNumbers;

/// <summary>How far <see cref="VatNumberValidationResult"/> got: only local checks, or VIES too.</summary>
public enum VatNumberValidationSource
{
    /// <summary>Only the module-11 structural check ran (individual NIF, or already invalid).</summary>
    LocalOnly,

    /// <summary>The structural check passed and the VIES REST API was queried.</summary>
    Vies
}

/// <summary>
/// Result of validating a Portuguese NIF/NIPC: the local structural check, and — for company
/// prefixes only — whatever VIES answered.
/// </summary>
public sealed record VatNumberValidationResult(
    bool IsStructurallyValid,
    VatNumberValidationSource Source,
    bool? IsRegisteredInVies = null,
    string? RegisteredName = null,
    string? RegisteredAddress = null,
    string? RegisteredPostalCode = null,
    string? RegisteredCity = null);
