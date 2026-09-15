namespace Erp.Main.Models.Core;

/// <summary>
/// Fills in a code for the forms where entering one is optional (customers, families,
/// subfamilies, brands) — the API still requires a non-empty, unique-per-company code, so
/// something has to stand in for a user who left the field blank.
/// </summary>
public static class CodeGenerator
{
    public static string Generate() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
