using System.Net;
using Erp.Dependencies.PostalCodes;
using FluentAssertions;

namespace Erp.Dependencies.Tests;

/// <summary>
/// The moradas.dev lookup that fills locality/municipality from a "0000-000" postal code. Unlike
/// the previous provider (geoapi.pt), moradas.dev always answers a single JSON object — never an
/// array — and has no freguesia (parish) field at all.
/// </summary>
public class PostalCodeLookupServiceTests
{
    private static PostalCodeLookupService CreateService(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://moradas.dev/") });

    [Fact]
    public async Task LookupAsync_calls_the_cp_endpoint_with_the_normalized_code()
    {
        var handler = new FakeHttpMessageHandler()
            .Returns(HttpStatusCode.OK, """{"cp7":"4805-476","cp4":"4805","cp3":"476","distrito":"Braga","concelho":"Guimarães","localidade":"Guimarães","arterias":[]}""");

        await CreateService(handler).LookupAsync(" 4805-476 ");

        handler.Requests.Should().ContainSingle().Which.RequestUri!.AbsolutePath.Should().Be("/cp/4805-476");
    }

    [Fact]
    public async Task LookupAsync_maps_the_single_object_response()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """{"cp7":"1000-001","cp4":"1000","cp3":"001","distrito":"Lisboa","concelho":"Lisboa","localidade":"Lisboa","arterias":[]}""");

        var result = await CreateService(handler).LookupAsync("1000-001");

        result.Should().BeEquivalentTo(new PostalCodeLookupResult("1000-001", "Lisboa", "Lisboa", "Lisboa", Parish: null, Streets: []));
    }

    /// <summary>moradas.dev has no freguesia field, so Parish is always null regardless of the response.</summary>
    [Fact]
    public async Task LookupAsync_always_leaves_the_parish_null()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """{"cp7":"4715-293","cp4":"4715","cp3":"293","distrito":"Braga","concelho":"Braga","localidade":"Braga","arterias":[]}""");

        var result = await CreateService(handler).LookupAsync("4715-293");

        result!.Parish.Should().BeNull();
    }

    [Fact]
    public async Task LookupAsync_maps_the_streets_moradas_dev_lists_for_the_code()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """
            {"cp7":"4700-001","cp4":"4700","cp3":"001","distrito":"Braga","concelho":"Braga","localidade":"Braga","arterias":[
                {"street":"Rua Costa Soares","troco":null},
                {"street":"Rua Doutor Carlos Magalhães","troco":"Impares de 1 a 19"}
            ]}
            """);

        var result = await CreateService(handler).LookupAsync("4700-001");

        result!.Streets.Should().BeEquivalentTo(
        [
            new PostalCodeStreet("Rua Costa Soares", null),
            new PostalCodeStreet("Rua Doutor Carlos Magalhães", "Impares de 1 a 19")
        ]);
    }

    /// <summary>moradas.dev sometimes repeats the exact same street when door-number ranges collide.</summary>
    [Fact]
    public async Task LookupAsync_collapses_duplicate_street_entries()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """
            {"cp7":"4715-293","cp4":"4715","cp3":"293","distrito":"Braga","concelho":"Braga","localidade":"Braga","arterias":[
                {"street":"Praça Ricardo da Rocha","troco":null},
                {"street":"Praça Ricardo da Rocha","troco":null}
            ]}
            """);

        var result = await CreateService(handler).LookupAsync("4715-293");

        result!.Streets.Should().ContainSingle().Which.Should().Be(new PostalCodeStreet("Praça Ricardo da Rocha", null));
    }

    [Fact]
    public async Task LookupAsync_returns_null_when_moradas_has_no_match()
    {
        var handler = new FakeHttpMessageHandler().Returns(HttpStatusCode.NotFound, """{"error":"not found"}""");

        var result = await CreateService(handler).LookupAsync("0000-000");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupAsync_returns_null_for_an_empty_body()
    {
        var handler = new FakeHttpMessageHandler().Returns(HttpStatusCode.OK, string.Empty);

        var result = await CreateService(handler).LookupAsync("1000-001");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LookupAsync_lets_a_server_error_surface_instead_of_hiding_it_as_no_match()
    {
        var handler = new FakeHttpMessageHandler().Returns(HttpStatusCode.InternalServerError);

        var act = () => CreateService(handler).LookupAsync("1000-001");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>A code that is not even "0000-000" is refused locally — moradas.dev is never asked.</summary>
    [Theory]
    [InlineData("1000001")]
    [InlineData("1000-0011")]
    [InlineData("ABCD-123")]
    public async Task LookupAsync_refuses_a_code_that_is_not_the_Portuguese_shape_without_a_network_call(string postalCode)
    {
        var handler = new FakeHttpMessageHandler();

        var result = await CreateService(handler).LookupAsync(postalCode);

        result.Should().BeNull();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_refuses_a_blank_code()
    {
        var act = () => CreateService(new FakeHttpMessageHandler()).LookupAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
