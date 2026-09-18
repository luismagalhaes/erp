using System.Net;
using Erp.Dependencies.VatNumbers;
using FluentAssertions;

namespace Erp.Dependencies.Tests;

/// <summary>
/// Validating a NIF/NIPC: the module-11 check runs first, for free, and only a company prefix
/// that passes it is worth a call to VIES — an individual, or an already-malformed NIF, never
/// reaches the network.
/// </summary>
public class VatNumberValidationServiceTests
{
    private static VatNumberValidationService CreateService(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/rest-api/") });

    [Fact]
    public async Task A_malformed_NIF_is_refused_locally()
    {
        var handler = new FakeHttpMessageHandler();

        var result = await CreateService(handler).ValidateAsync("12345");

        result.IsStructurallyValid.Should().BeFalse();
        result.Source.Should().Be(VatNumberValidationSource.LocalOnly);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task An_individual_NIF_is_accepted_without_calling_VIES()
    {
        var handler = new FakeHttpMessageHandler();

        var result = await CreateService(handler).ValidateAsync("123456789");

        result.IsStructurallyValid.Should().BeTrue();
        result.Source.Should().Be(VatNumberValidationSource.LocalOnly);
        result.IsRegisteredInVies.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_company_NIF_is_looked_up_in_VIES_for_its_own_country()
    {
        var handler = new FakeHttpMessageHandler()
            .Returns(HttpStatusCode.OK, """{"isValid":true,"name":"Empresa Teste, Lda","address":"Rua Um, Lisboa"}""");

        await CreateService(handler).ValidateAsync(" PT 500 000 000 ");

        handler.Requests.Should().ContainSingle().Which.RequestUri!.AbsolutePath
            .Should().Be("/taxation_customs/vies/rest-api/ms/PT/vat/500000000");
    }

    /// <summary>VIES always answers a Portuguese address as three lines — street, postal code,
    /// locality — never as one block, so the result has to split it into the three fields a
    /// customer/supplier form actually has.</summary>
    [Fact]
    public async Task A_company_NIF_registered_in_VIES_comes_back_with_its_address_split_in_three()
    {
        var handler = new FakeHttpMessageHandler()
            .Returns(HttpStatusCode.OK, """{"isValid":true,"name":"Empresa Teste, Lda","address":"Rua Um, 123\n1000-001\nLisboa"}""");

        var result = await CreateService(handler).ValidateAsync("500000000");

        result.IsStructurallyValid.Should().BeTrue();
        result.Source.Should().Be(VatNumberValidationSource.Vies);
        result.IsRegisteredInVies.Should().BeTrue();
        result.RegisteredName.Should().Be("Empresa Teste, Lda");
        result.RegisteredAddress.Should().Be("Rua Um, 123");
        result.RegisteredPostalCode.Should().Be("1000-001");
        result.RegisteredCity.Should().Be("Lisboa");
    }

    /// <summary>Structurally sound, but VIES has never heard of it — a real, informative outcome.</summary>
    [Fact]
    public async Task A_company_NIF_not_registered_in_VIES_is_reported_as_such()
    {
        var handler = new FakeHttpMessageHandler().Returns(HttpStatusCode.OK, """{"isValid":false}""");

        var result = await CreateService(handler).ValidateAsync("500000000");

        result.IsStructurallyValid.Should().BeTrue();
        result.IsRegisteredInVies.Should().BeFalse();
        result.RegisteredName.Should().BeNull();
    }
}
