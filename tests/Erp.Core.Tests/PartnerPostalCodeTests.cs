using Erp.Core.Application.Services;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;
using FluentAssertions;
using NSubstitute;

namespace Erp.Core.Tests;

/// <summary>
/// The Portuguese postal code format, which the SAF-T and the AT webservices both expect.
/// </summary>
public class PartnerPostalCodeTests
{
    private readonly ICustomerStorage _storage = Substitute.For<ICustomerStorage>();

    private CustomerService CreateService() => new(_storage);

    private static CreatePartnerRequest Request(string? postalCode, string country = "PT") =>
        new(Guid.NewGuid(), "1", "Cliente Teste", "500123456", PostalCode: postalCode, Country: country);

    [Theory]
    [InlineData("1000-001")]
    [InlineData(" 4450-123 ")]
    [InlineData(null)]
    [InlineData("")]
    public async Task A_valid_or_empty_portuguese_postal_code_is_accepted(string? postalCode)
    {
        var created = await CreateService().CreateAsync(Request(postalCode));

        created.Name.Should().Be("Cliente Teste");
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("1000001")]
    [InlineData("1000-01")]
    [InlineData("ABCD-123")]
    public async Task A_portuguese_postal_code_of_another_shape_is_refused(string postalCode)
    {
        var act = () => CreateService().CreateAsync(Request(postalCode));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*XXXX-XXX*");
    }

    /// <summary>Other countries write their codes their own way, so nothing is imposed on them.</summary>
    [Fact]
    public async Task A_foreign_postal_code_is_taken_as_written()
    {
        var created = await CreateService().CreateAsync(Request("28013", country: "ES"));

        created.Country.Should().Be("ES");
    }
}
