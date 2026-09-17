using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.AtWebservice.Crypto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>
/// SOAP client for AT's "Comunicação de Séries Documentais" webservice (<c>SeriesWSService</c>):
/// <c>registarSerie</c>, <c>anularSerie</c>, <c>finalizarSerie</c>. Built by hand over
/// <see cref="HttpClient"/>, same shape as <see cref="TransportDocuments.AtTransportDocumentClient"/> — the WS-Security
/// header is byte-for-byte the same scheme, reused via <see cref="WsSecurityHeaderBuilder"/>. Element
/// names and the target namespace (<c>http://at.gov.pt/</c>) come from the real WSDL, not just the
/// manual's prose.
/// </summary>
public sealed class AtSeriesClient(
    HttpClient httpClient,
    IOptions<AtOptions> options,
    TimeProvider timeProvider,
    ILogger<AtSeriesClient> logger) : IAtSeriesClient
{
    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace WsseNs = "http://schemas.xmlsoap.org/ws/2002/12/secext";
    private static readonly XNamespace Tns = "http://at.gov.pt/";

    // The pattern observed across the operations the manual documents in full (anular=2003,
    // finalizar=2004) is one "2XXX" success code per operation, in declaration order — registar's
    // own table came out of the PDF extraction with its columns scrambled, so 2001 here is inferred
    // from that pattern, not read verbatim. Verify against the AT test environment once credentials
    // exist.
    private const int RegisterSuccessCode = 2001;
    private const int CancelSuccessCode = 2003;
    private const int FinalizeSuccessCode = 2004;

    private readonly AtOptions _options = options.Value;

    public Task<AtSeriesOperationResult> RegisterAsync(
        AtSeriesRegistrationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fields = new[]
        {
            new XElement("serie", request.Serie),
            new XElement("tipoSerie", request.TipoSerie),
            new XElement("classeDoc", request.ClasseDoc),
            new XElement("tipoDoc", request.TipoDoc),
            new XElement("numInicialSeq", request.NumInicialSeq),
            new XElement("dataInicioPrevUtiliz", request.DataInicioPrevUtiliz.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement("numCertSWFatur", request.NumCertSWFatur),
            new XElement("meioProcessamento", request.MeioProcessamento)
        };

        return InvokeAsync("registarSerie", fields, RegisterSuccessCode, credentials, cancellationToken);
    }

    public Task<AtSeriesOperationResult> CancelAsync(
        AtSeriesCancellationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fields = new[]
        {
            new XElement("serie", request.Serie),
            new XElement("classeDoc", request.ClasseDoc),
            new XElement("tipoDoc", request.TipoDoc),
            new XElement("codValidacaoSerie", request.CodValidacaoSerie),
            new XElement("motivo", request.Motivo),
            new XElement("declaracaoNaoEmissao", request.DeclaracaoNaoEmissao ? "true" : "false")
        };

        return InvokeAsync("anularSerie", fields, CancelSuccessCode, credentials, cancellationToken);
    }

    public Task<AtSeriesOperationResult> FinalizeAsync(
        AtSeriesFinalizationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fields = new List<XElement>
        {
            new("serie", request.Serie),
            new("classeDoc", request.ClasseDoc),
            new("tipoDoc", request.TipoDoc),
            new("codValidacaoSerie", request.CodValidacaoSerie),
            new("seqUltimoDocEmitido", request.SeqUltimoDocEmitido)
        };

        if (!string.IsNullOrWhiteSpace(request.Justificacao))
            fields.Add(new XElement("justificacao", request.Justificacao));

        return InvokeAsync("finalizarSerie", fields, FinalizeSuccessCode, credentials, cancellationToken);
    }

    private async Task<AtSeriesOperationResult> InvokeAsync(
        string operation,
        IEnumerable<XElement> requestFields,
        int successCode,
        AtCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicKeyPem))
            throw new InvalidOperationException("AT:PublicKeyPem is not configured.");

        var header = WsSecurityHeaderBuilder.Build(
            $"{credentials.TaxRegistrationNumber}/{credentials.SubUserId}",
            credentials.Password,
            _options.PublicKeyPem,
            timeProvider.GetUtcNow().UtcDateTime);

        var envelope = BuildEnvelope(header, operation, requestFields);

        using var content = new StringContent(envelope.ToString(SaveOptions.DisableFormatting), Encoding.UTF8, "text/xml");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = content };

        // The WSDL declares soapAction="" for every operation — an empty header, not an omitted one.
        httpRequest.Headers.TryAddWithoutValidation("SOAPAction", "\"\"");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "AT series webservice returned HTTP {StatusCode} for {Operation}.", response.StatusCode, operation);
        }

        return ParseResponse(responseBody, operation, successCode);
    }

    private static XDocument BuildEnvelope(WsSecurityHeader header, string operation, IEnumerable<XElement> requestFields)
    {
        var security =
            new XElement(WsseNs + "Security",
                new XElement(WsseNs + "UsernameToken",
                    new XElement(WsseNs + "Username", header.Username),
                    new XElement(WsseNs + "Password", header.Password),
                    new XElement(WsseNs + "Nonce", header.Nonce),
                    new XElement(WsseNs + "Created", header.Created)));

        var body = new XElement(Tns + operation, requestFields);

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(SoapNs + "Envelope",
                new XElement(SoapNs + "Header", security),
                new XElement(SoapNs + "Body", body)));
    }

    private static AtSeriesOperationResult ParseResponse(string xml, string operation, int successCode)
    {
        var document = XDocument.Parse(xml);

        var fault = document.Descendants(SoapNs + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultString = fault.Element("faultstring")?.Value ?? fault.ToString();
            throw new AtSeriesException(-1, faultString);
        }

        var responseElement = document.Descendants(Tns + $"{operation}Response").FirstOrDefault()
            ?? throw new InvalidOperationException($"Unexpected AT response: no {operation}Response element found.");

        var resp = responseElement.Element($"{operation}Resp")
            ?? throw new InvalidOperationException($"Unexpected AT response: no {operation}Resp element found.");

        var status = resp.Element("infoResultOper")
            ?? throw new InvalidOperationException("Unexpected AT response: no infoResultOper element found.");

        var returnCode = int.Parse(status.Element("codResultOper")?.Value ?? "0", CultureInfo.InvariantCulture);
        var returnMessage = status.Element("msgResultOper")?.Value;

        if (returnCode != successCode)
            throw new AtSeriesException(returnCode, returnMessage);

        var info = resp.Element("infoSerie");

        return new AtSeriesOperationResult(
            ReturnCode: returnCode,
            ReturnMessage: returnMessage,
            ValidationCode: info?.Element("codValidacaoSerie")?.Value,
            Estado: info?.Element("estado")?.Value);
    }
}
