using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

public class DocumentIdentifierTests
{
    [Fact]
    public void DocumentNumber_uses_the_saft_invoice_no_format()
    {
        DocumentNumber.Build(SalesDocumentTypes.Invoice, "A2026", 2).Should().Be("FT A2026/2");
    }

    [Fact]
    public void Atcud_joins_the_validation_code_and_the_sequence()
    {
        Atcud.Build("JFTX7RK9", 2).Should().Be("JFTX7RK9-2");
        Atcud.BuildPrintable("JFTX7RK9", 2).Should().Be("ATCUD:JFTX7RK9-2");
    }

    [Fact]
    public void Atcud_rejects_a_sequence_below_one()
    {
        var act = () => Atcud.Build("JFTX7RK9", 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(-10.005, -10.01)]
    public void FiscalRounding_rounds_away_from_zero_at_two_decimals(decimal value, decimal expected)
    {
        FiscalRounding.Amount(value).Should().Be(expected);
    }
}
