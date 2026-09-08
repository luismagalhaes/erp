namespace Erp.FiscalPT.Saft;

/// <summary>Who issued a document, in the shape the SourceID field accepts.</summary>
public static class SaftSourceId
{
    /// <summary>Longest SourceID the official schema allows.</summary>
    private const int MaxLength = 30;

    /// <summary>
    /// Fits a user id into the field. Identity issues GUIDs of 36 characters and the schema allows
    /// 30, so it is truncated rather than left to fail validation on every single document.
    /// </summary>
    public static string Fit(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return SaftConstants.Unknown;

        return userId.Length > MaxLength ? userId[..MaxLength] : userId;
    }
}
