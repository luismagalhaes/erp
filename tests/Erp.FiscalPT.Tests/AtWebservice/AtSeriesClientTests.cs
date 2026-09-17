using System.Security.Cryptography;
using Erp.FiscalPT.AtWebservice;
using Erp.FiscalPT.AtWebservice.Series;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Erp.FiscalPT.Tests.AtWebservice;

public class AtSeriesClientTests
{
    private static readonly string PublicKeyPem = GeneratePublicKeyPem();

    [Fact]
    public async Task RegisterAsync_returns_the_validation_code_on_success()
    {
        var handler = new StubHttpMessageHandler(
            ResponseXml("registarSerie", 2001, null, "<codValidacaoSerie>JFTX7RK9</codValidacaoSerie><estado>A</estado>"));

        var result = await CreateClient(handler).RegisterAsync(SampleRegistration(), SampleCredentials());

        result.ReturnCode.Should().Be(2001);
        result.ValidationCode.Should().Be("JFTX7RK9");
        result.Estado.Should().Be("A");
    }

    [Fact]
    public async Task RegisterAsync_throws_on_a_rejection()
    {
        var handler = new StubHttpMessageHandler(ResponseXml("registarSerie", 4043, "Série já registada", null));

        var act = () => CreateClient(handler).RegisterAsync(SampleRegistration(), SampleCredentials());

        var assertion = await act.Should().ThrowAsync<AtSeriesException>();
        assertion.Which.ReturnCode.Should().Be(4043);
        assertion.Which.ReturnMessage.Should().Be("Série já registada");
    }

    [Fact]
    public async Task CancelAsync_succeeds_on_the_documented_code()
    {
        var handler = new StubHttpMessageHandler(ResponseXml("anularSerie", 2003, "Série anulada com sucesso.", null));

        var result = await CreateClient(handler).CancelAsync(SampleCancellation(), SampleCredentials());

        result.ReturnCode.Should().Be(2003);
    }

    [Fact]
    public async Task CancelAsync_sends_the_no_emission_declaration()
    {
        var handler = new StubHttpMessageHandler(ResponseXml("anularSerie", 2003, null, null));

        await CreateClient(handler).CancelAsync(SampleCancellation(), SampleCredentials());

        handler.LastRequestBody.Should().Contain(">true<");
        handler.LastRequestBody.Should().Contain(">ER<");
    }

    [Fact]
    public async Task FinalizeAsync_succeeds_on_the_documented_code()
    {
        var handler = new StubHttpMessageHandler(ResponseXml("finalizarSerie", 2004, "Série finalizada com sucesso.", null));

        var result = await CreateClient(handler).FinalizeAsync(SampleFinalization(), SampleCredentials());

        result.ReturnCode.Should().Be(2004);
    }

    [Fact]
    public async Task FinalizeAsync_omits_justificacao_when_not_given()
    {
        var handler = new StubHttpMessageHandler(ResponseXml("finalizarSerie", 2004, null, null));

        await CreateClient(handler).FinalizeAsync(SampleFinalization() with { Justificacao = null }, SampleCredentials());

        handler.LastRequestBody.Should().NotContain("justificacao");
    }

    private static AtSeriesClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/SeriesWSService") };
        var options = Options.Create(new AtOptions { PublicKeyPem = PublicKeyPem });

        return new AtSeriesClient(httpClient, options, TimeProvider.System, NullLogger<AtSeriesClient>.Instance);
    }

    private static AtSeriesRegistrationRequest SampleRegistration() => new(
        Serie: "A2026",
        TipoSerie: "N",
        ClasseDoc: "SI",
        TipoDoc: "FT",
        NumInicialSeq: 1,
        DataInicioPrevUtiliz: new DateOnly(2026, 1, 1),
        NumCertSWFatur: 0,
        MeioProcessamento: "PI");

    private static AtSeriesCancellationRequest SampleCancellation() => new(
        Serie: "A2026",
        ClasseDoc: "SI",
        TipoDoc: "FT",
        CodValidacaoSerie: "JFTX7RK9",
        Motivo: "ER",
        DeclaracaoNaoEmissao: true);

    private static AtSeriesFinalizationRequest SampleFinalization() => new(
        Serie: "A2026",
        ClasseDoc: "SI",
        TipoDoc: "FT",
        CodValidacaoSerie: "JFTX7RK9",
        SeqUltimoDocEmitido: 42,
        Justificacao: "fim de ano");

    private static AtCredentials SampleCredentials() => new("500123456", "1", "s3cr3t");

    /// <summary>
    /// Mirrors a prefixed-wrapper, unqualified-children SOAP response, the same shape used for the
    /// transport documents client's tests (and, per the manual, the same WS-Security scheme).
    /// </summary>
    private static string ResponseXml(string operation, int returnCode, string? message, string? extraInfoSerieXml) =>
        $"""
        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns:tns="http://at.gov.pt/">
          <soap:Body>
            <tns:{operation}Response>
              <{operation}Resp>
                {(extraInfoSerieXml is null ? "" : $"<infoSerie>{extraInfoSerieXml}</infoSerie>")}
                <infoResultOper>
                  <codResultOper>{returnCode}</codResultOper>
                  {(message is null ? "" : $"<msgResultOper>{message}</msgResultOper>")}
                </infoResultOper>
              </{operation}Resp>
            </tns:{operation}Response>
          </soap:Body>
        </soap:Envelope>
        """;

    private static string GeneratePublicKeyPem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }
}
