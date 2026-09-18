using Erp.Dependencies.VatNumbers;
using FluentAssertions;

namespace Erp.Dependencies.Tests;

/// <summary>
/// VIES sends a Portuguese company's address as one free-text block: the street address, a blank
/// line, then the postal code and the locality together on the last line — never as separate
/// fields of its own.
/// </summary>
public class ViesAddressTests
{
    /// <summary>The shape VIES actually sends, confirmed against a real lookup: street, a blank
    /// line, then "postal code locality" together on the last one.</summary>
    [Fact]
    public void Splits_the_shape_VIES_actually_sends()
    {
        var (address, postalCode, city) = ViesAddress.Parse("PC PADRE RICARDO ROCHA N 38 R/C\n\n4715-293 BRAGA");

        address.Should().Be("PC PADRE RICARDO ROCHA N 38 R/C");
        postalCode.Should().Be("4715-293");
        city.Should().Be("BRAGA");
    }

    /// <summary>A street address spanning more than one line still leaves only the last line split
    /// into postal code and locality — everything before it joins back into the address.</summary>
    [Fact]
    public void Joins_a_multi_line_street_address_back_together()
    {
        var (address, postalCode, city) = ViesAddress.Parse("Rua Um, 123\nEdifício A, 2.º Andar\n\n1000-001 LISBOA");

        address.Should().Be("Rua Um, 123, Edifício A, 2.º Andar");
        postalCode.Should().Be("1000-001");
        city.Should().Be("LISBOA");
    }

    /// <summary>Accepted too, in case the postal code and the locality ever arrive on lines of
    /// their own instead of sharing the last one.</summary>
    [Fact]
    public void Also_accepts_the_postal_code_on_a_line_of_its_own()
    {
        var (address, postalCode, city) = ViesAddress.Parse("Rua Um, 123\n1000-001\nLisboa");

        address.Should().Be("Rua Um, 123");
        postalCode.Should().Be("1000-001");
        city.Should().Be("Lisboa");
    }

    /// <summary>Not a recognised shape — the whole block stays the address rather than guessing
    /// which part is which.</summary>
    [Fact]
    public void Leaves_a_single_line_address_alone()
    {
        var (address, postalCode, city) = ViesAddress.Parse("Rua Um, Lisboa");

        address.Should().Be("Rua Um, Lisboa");
        postalCode.Should().BeNull();
        city.Should().BeNull();
    }

    /// <summary>VIES is not guaranteed to send a bare "0000-000" — a country prefix or a space
    /// instead of the dash still counts, rebuilt as the plain "0000-000" shape the app needs.</summary>
    [Theory]
    [InlineData("PT-1000-001 LISBOA")]
    [InlineData("P-1000-001 LISBOA")]
    [InlineData("1000 001 LISBOA")]
    [InlineData("1000001 LISBOA")]
    public void Finds_the_postal_code_even_with_a_prefix_or_a_different_separator(string lastLine)
    {
        var (address, postalCode, city) = ViesAddress.Parse($"Rua Um, 123\n\n{lastLine}");

        address.Should().Be("Rua Um, 123");
        postalCode.Should().Be("1000-001");
        city.Should().Be("LISBOA");
    }

    /// <summary>The last line can be the postal code alone, with no locality after it — the
    /// address and the code still split out, there is just nothing left for the city.</summary>
    [Fact]
    public void Leaves_the_city_blank_when_the_last_line_is_only_the_postal_code()
    {
        var (address, postalCode, city) = ViesAddress.Parse("Rua Um, 123\n\n1000-001");

        address.Should().Be("Rua Um, 123");
        postalCode.Should().Be("1000-001");
        city.Should().BeNull();
    }

    /// <summary>Two lines, neither of them a postal code — still not worth guessing at.</summary>
    [Fact]
    public void Leaves_a_two_line_address_alone_when_neither_line_is_a_postal_code()
    {
        var (address, postalCode, city) = ViesAddress.Parse("Rua Um, 123\nLisboa");

        address.Should().Be("Rua Um, 123 Lisboa");
        postalCode.Should().BeNull();
        city.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_nothing_for_a_blank_address(string? raw)
    {
        var (address, postalCode, city) = ViesAddress.Parse(raw);

        address.Should().BeNull();
        postalCode.Should().BeNull();
        city.Should().BeNull();
    }
}
