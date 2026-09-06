using System.Globalization;
using Erp.FiscalPT.Signing;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class DocumentSignatureStringTests
{
    [Fact]
    public void Build_uses_the_format_required_by_portaria_363_2010()
    {
        var result = DocumentSignatureString.Build(
            new DateOnly(2026, 1, 15),
            new DateTime(2026, 1, 15, 10, 32, 4, DateTimeKind.Utc),
            "FT A2026/2",
            250.00m,
            "kQ7v");

        result.Should().Be("2026-01-15;2026-01-15T10:32:04;FT A2026/2;250.00;kQ7v");
    }

    [Fact]
    public void Build_leaves_the_previous_hash_empty_for_the_first_document_of_a_series()
    {
        var result = DocumentSignatureString.Build(
            new DateOnly(2026, 1, 1),
            new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
            "FT A2026/1",
            100m,
            null);

        result.Should().EndWith("FT A2026/1;100.00;");
    }

    [Fact]
    public void Build_formats_amounts_with_a_decimal_point_under_a_portuguese_culture()
    {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-PT");

        try
        {
            var result = DocumentSignatureString.Build(
                new DateOnly(2026, 3, 4),
                new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc),
                "FT A2026/7",
                1234.5m,
                string.Empty);

            result.Should().Contain(";1234.50;");
            result.Should().NotContain(",");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Build_rejects_an_empty_document_number()
    {
        var act = () => DocumentSignatureString.Build(
            new DateOnly(2026, 1, 1),
            DateTime.UtcNow,
            string.Empty,
            10m,
            null);

        act.Should().Throw<ArgumentException>();
    }
}
