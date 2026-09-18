using Erp.FiscalPT.Documents;
using FluentAssertions;

namespace Erp.FiscalPT.Tests;

/// <summary>The AT's table of VAT exemption reasons, as the documents use it.</summary>
public class TaxExemptionReasonsTests
{
    /// <summary>What is exported must fit SAF-T, or the file stops validating.</summary>
    [Fact]
    public void Every_legal_basis_fits_the_SAF_T_field()
    {
        TaxExemptionReasons.All.Should().OnlyContain(reason =>
            reason.LegalBasis.Length <= TaxExemptionReasons.MaxReasonLength);
    }

    [Fact]
    public void Codes_are_unique_and_follow_the_M_pattern()
    {
        TaxExemptionReasons.All.Select(reason => reason.Code).Should().OnlyHaveUniqueItems()
            .And.OnlyContain(code => code.Length == 3 && code[0] == 'M' && char.IsDigit(code[1]) && char.IsDigit(code[2]));
    }

    [Fact]
    public void The_codes_added_since_2023_are_present()
    {
        TaxExemptionReasons.Find("M44").Should().NotBeNull();
        TaxExemptionReasons.Find("M45").Should().NotBeNull();
        TaxExemptionReasons.Find("M46").Should().NotBeNull();
    }

    [Fact]
    public void An_exempt_line_without_a_code_is_refused()
    {
        var (_, _, error) = TaxExemptionReasons.Resolve(TaxCodes.Exempt, null, "Isento");

        error.Should().Contain("exemption code");
    }

    [Fact]
    public void A_reason_given_by_the_caller_is_kept()
    {
        var (code, reason, error) = TaxExemptionReasons.Resolve(TaxCodes.Exempt, "M16", "Artigo 14.º, n.º 1, alínea a) do RITI");

        error.Should().BeNull();
        code.Should().Be("M16");
        reason.Should().Be("Artigo 14.º, n.º 1, alínea a) do RITI");
    }

    [Fact]
    public void A_reason_longer_than_SAF_T_allows_is_refused()
    {
        var (_, _, error) = TaxExemptionReasons.Resolve(TaxCodes.Exempt, "M99", new string('x', 61));

        error.Should().Contain("60 characters");
    }
}
