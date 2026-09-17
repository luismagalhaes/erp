using System.Net;
using System.Security.Cryptography;
using System.Text;
using Erp.FiscalPT.AtWebservice;
using Erp.FiscalPT.AtWebservice.TransportDocuments;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Erp.FiscalPT.Tests.AtWebservice;

public class AtTransportDocumentClientTests
{
    private static readonly string PublicKeyPem = GeneratePublicKeyPem();

    [Fact]
    public async Task CommunicateAsync_returns_success_and_the_at_code()
    {
        var handler = new StubHttpMessageHandler(ResponseXml(0, null, "ABC123"));

        var result = await CreateClient(handler).CommunicateAsync(SampleRequest(), SampleCredentials());

        result.Success.Should().BeTrue();
        result.IsAlert.Should().BeFalse();
        result.AtDocCodeId.Should().Be("ABC123");
    }

    [Fact]
    public async Task CommunicateAsync_treats_code_minus_100_as_a_successful_alert()
    {
        var handler = new StubHttpMessageHandler(ResponseXml(-100, "late communication", "ABC123"));

        var result = await CreateClient(handler).CommunicateAsync(SampleRequest(), SampleCredentials());

        result.Success.Should().BeTrue();
        result.IsAlert.Should().BeTrue();
        result.ReturnCode.Should().Be(-100);
    }

    [Fact]
    public async Task CommunicateAsync_throws_with_ats_code_and_message_on_rejection()
    {
        var handler = new StubHttpMessageHandler(ResponseXml(-7, "NIF mismatch", null));

        var act = () => CreateClient(handler).CommunicateAsync(SampleRequest(), SampleCredentials());

        var assertion = await act.Should().ThrowAsync<AtTransportDocumentException>();
        assertion.Which.ReturnCode.Should().Be(-7);
        assertion.Which.ReturnMessage.Should().Be("NIF mismatch");
    }

    [Fact]
    public async Task CommunicateAsync_sends_the_username_as_taxid_slash_subuser()
    {
        var handler = new StubHttpMessageHandler(ResponseXml(0, null, "ABC123"));

        await CreateClient(handler).CommunicateAsync(SampleRequest(), SampleCredentials());

        handler.LastRequestBody.Should().Contain("<Username>500123456/1</Username>");
    }

    [Fact]
    public async Task CommunicateAsync_picks_supplier_tax_id_when_the_party_is_a_supplier()
    {
        var handler = new StubHttpMessageHandler(ResponseXml(0, null, "ABC123"));
        var request = SampleRequest() with { PartyIsSupplier = true, PartyTaxId = "500999999" };

        await CreateClient(handler).CommunicateAsync(request, SampleCredentials());

        handler.LastRequestBody.Should().Contain(">500999999</SupplierTaxID>");
        handler.LastRequestBody.Should().NotContain("CustomerTaxID");
    }

    private static AtTransportDocumentClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/sgdtws/documentosTransporte")
        };
        var options = Options.Create(new AtOptions { PublicKeyPem = PublicKeyPem });

        return new AtTransportDocumentClient(
            httpClient, options, TimeProvider.System, NullLogger<AtTransportDocumentClient>.Instance);
    }

    private static AtTransportDocumentRequest SampleRequest() => new(
        IssuerTaxId: "500123456",
        CompanyName: "Acme",
        CompanyAddress: new AtTransportDocumentAddress("Rua A", "Lisboa", "1000-000"),
        DocumentNumber: "GT A2026/2",
        Atcud: "JFTX7RK9-2",
        MovementStatus: "N",
        MovementDate: new DateOnly(2026, 1, 1),
        MovementType: "GT",
        PartyTaxId: "999999990",
        PartyIsSupplier: false,
        PartyName: "Cliente Final",
        ShipTo: new AtTransportDocumentAddress("Rua B", "Porto", "4000-000"),
        ShipFrom: new AtTransportDocumentAddress("Rua A", "Lisboa", "1000-000"),
        MovementEndAtUtc: null,
        MovementStartAtUtc: new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc),
        VehiclePlate: "00-AA-00",
        Lines: [new AtTransportDocumentLine("Produto", 1, "UN", 10)]);

    private static AtCredentials SampleCredentials() => new("500123456", "1", "s3cr3t");

    /// <summary>
    /// Mirrors the shape a JAX-WS/Axis style server — which is what AT's own SOAP:Header example in
    /// the manual looks like — actually emits for an elementFormDefault="unqualified" schema: a
    /// prefixed wrapper, plain unqualified children.
    /// </summary>
    private static string ResponseXml(int returnCode, string? message, string? atDocCodeId) =>
        $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tns="https://servicos.portaldasfinancas.gov.pt/sgdtws/documentosTransporte/">
          <soap:Body>
            <tns:envioDocumentoTransporteResponseElem>
              <ResponseStatus>
                <ReturnCode>{returnCode}</ReturnCode>
                {(message is null ? "" : $"<ReturnMessage>{message}</ReturnMessage>")}
              </ResponseStatus>
              {(atDocCodeId is null ? "" : $"<ATDocCodeID>{atDocCodeId}</ATDocCodeID>")}
            </tns:envioDocumentoTransporteResponseElem>
          </soap:Body>
        </soap:Envelope>
        """;

    private static string GeneratePublicKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }
}

/// <summary>Records the outgoing request and answers with a canned body — no mocking library needed for this.</summary>
internal sealed class StubHttpMessageHandler(string responseXml) : HttpMessageHandler
{
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseXml, Encoding.UTF8, "text/xml")
        };
    }
}
