using Erp.Dependencies.VatNumbers;
using FluentAssertions;

namespace Erp.Dependencies.Tests;

/// <summary>
/// The module-11 check that gates every NIF/NIPC before it is worth a call to VIES. Never touches
/// the network, so a wrong digit here would fail silently — nothing downstream re-checks it.
/// </summary>
public class NifValidatorTests
{
    [Theory]
    [InlineData("123456789", "individual — prefix 1")]
    [InlineData("500000000", "sociedade — single-digit prefix 5")]
    [InlineData("700000003", "herança indivisa — two-digit prefix 70")]
    [InlineData("450000001", "two-digit prefix 45")]
    public void A_well_formed_NIF_is_valid(string nif, string because)
    {
        NifValidator.IsValid(nif).Should().BeTrue(because);
    }

    [Theory]
    [InlineData("500000001", "wrong check digit")]
    [InlineData("400000000", "prefix 4 is not assigned to anything")]
    [InlineData("12345678", "only 8 digits")]
    [InlineData("1234567890", "10 digits")]
    [InlineData("12345678A", "a letter where a digit belongs")]
    [InlineData(null, "nothing to validate")]
    [InlineData("", "nothing to validate")]
    [InlineData("   ", "blank")]
    public void A_malformed_or_wrong_checksum_NIF_is_invalid(string? nif, string because)
    {
        NifValidator.IsValid(nif).Should().BeFalse(because);
    }

    [Fact]
    public void TryNormalize_strips_the_PT_prefix_and_internal_spaces()
    {
        var ok = NifValidator.TryNormalize("PT 500 000 000", out var normalized);

        ok.Should().BeTrue();
        normalized.Should().Be("500000000");
    }

    [Fact]
    public void TryNormalize_accepts_a_lower_case_pt_prefix()
    {
        var ok = NifValidator.TryNormalize("pt123456789", out var normalized);

        ok.Should().BeTrue();
        normalized.Should().Be("123456789");
    }

    [Fact]
    public void TryNormalize_refuses_something_that_is_not_nine_digits_once_normalized()
    {
        var ok = NifValidator.TryNormalize("PT12345", out _);

        ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("500000000", true, "prefix 5")]
    [InlineData("600000001", true, "prefix 6")]
    [InlineData("900000007", true, "two-digit prefix 90, first digit 9")]
    [InlineData("700000003", true, "two-digit prefix 70, first digit 7")]
    [InlineData("123456789", false, "an individual")]
    [InlineData("450000001", false, "two-digit prefix 45, but first digit is not 7 or 9")]
    public void IsCompanyPrefix_identifies_a_collective_entity(string nif, bool expected, string because)
    {
        NifValidator.IsCompanyPrefix(nif).Should().Be(expected, because);
    }

    [Fact]
    public void IsCompanyPrefix_is_false_for_a_NIF_that_does_not_even_normalize()
    {
        NifValidator.IsCompanyPrefix("not-a-nif").Should().BeFalse();
    }
}
