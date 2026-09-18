using System.Net;
using Erp.Dependencies.PostalCodes;
using FluentAssertions;

namespace Erp.Dependencies.Tests;

/// <summary>
/// The geoapi.pt lookup that fills locality/municipality from a "0000-000" postal code. geoapi.pt
/// answers a single object for a fully specific code and an array for a broader one, and this is
/// what tells the two shapes apart without the caller having to know which is coming.
/// </summary>
public class PostalCodeLookupServiceTests
{
    private static PostalCodeLookupService CreateService(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://json.geoapi.pt/") });

    [Fact]
    public async Task LookupAsync_calls_the_cp_endpoint_with_the_normalized_code()
    {
        var handler = new FakeHttpMessageHandler()
            .Returns(HttpStatusCode.OK, """{"CP":"4805-476","Localidade":"Guimarães"}""");

        await CreateService(handler).LookupAsync(" 4805-476 ");

        handler.Requests.Should().ContainSingle().Which.RequestUri!.AbsolutePath.Should().Be("/cp/4805-476");
    }

    [Fact]
    public async Task LookupAsync_maps_a_single_object_response()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """{"CP":"1000-001","Localidade":"Lisboa","Concelho":"Lisboa","Distrito":"Lisboa","Freguesia":"Santo António"}""");

        var result = await CreateService(handler).LookupAsync("1000-001");

        result.Should().Be(new PostalCodeLookupResult("1000-001", "Lisboa", "Lisboa", "Lisboa", "Santo António"));
    }

    /// <summary>A code that only pins down the CTT zone comes back as an array of matches.</summary>
    [Fact]
    public async Task LookupAsync_takes_the_first_match_of_an_array_response()
    {
        var handler = new FakeHttpMessageHandler().Returns(
            HttpStatusCode.OK,
            """[{"CP":"4700-000","Localidade":"Braga"},{"CP":"4700-001","Localidade":"Braga (outra rua)"}]""");

        var result = await CreateService(handler).LookupAsync("4700-000");

        result!.Locality.Should().Be("Braga");
    }

    [Fact]
    public async Task LookupAsync_returns_null_when_geoapi_has_no_match()
    {
        var handler = new FakeHttpMessageHandler().Returns(HttpStatusCode.NotFound);

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

    /// <summary>A code that is not even "0000-000" is refused locally — geoapi.pt is never asked.</summary>
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
