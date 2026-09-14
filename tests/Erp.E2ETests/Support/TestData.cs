namespace Erp.E2ETests.Support;

/// <summary>
/// Unique-enough identifiers for test data. Nothing this test suite creates against a shared
/// environment (staging) is ever cleaned up, so a suffix derived from a truncated timestamp
/// collides within minutes of running the same test repeatedly — a series code collision in
/// particular fails the whole request with a 500, since document type + code must be unique per
/// company. A GUID-derived suffix does not have that problem.
/// </summary>
public static class TestData
{
    public static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..10];

    /// <summary>A run of digits only, for fields like a tax id that are validated as numeric.</summary>
    public static string UniqueDigits(int length = 9)
    {
        var bytes = Guid.NewGuid().ToByteArray();
        var digits = new char[length];

        for (var i = 0; i < length; i++)
            digits[i] = (char)('0' + bytes[i % bytes.Length] % 10);

        return new string(digits);
    }
}
