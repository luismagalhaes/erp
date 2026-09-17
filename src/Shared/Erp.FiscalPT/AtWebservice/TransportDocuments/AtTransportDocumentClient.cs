using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.FiscalPT.AtWebservice.Crypto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>
/// SOAP client for AT's "Comunicação dos Documentos de Transporte" webservice
/// (<c>envioDocumentoTransporte</c>), built by hand over <see cref="HttpClient"/> — there is no
/// SOAP/WCF tooling in this solution, and the operation is a single document-literal call, not
/// worth pulling one in for. The exact element names and namespace come from the real WSDL
/// (fetched from the address in the AT manual), not just the manual's prose field list.
/// </summary>
public sealed class AtTransportDocumentClient(
    HttpClient httpClient,
    IOptions<AtOptions> options,
    TimeProvider timeProvider,
    ILogger<AtTransportDocumentClient> logger) : IAtTransportDocumentClient
{
    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace WsseNs = "http://schemas.xmlsoap.org/ws/2002/12/secext";

    // The WSDL's targetNamespace; also the SOAPAction (AT reuses it for both).
    private static readonly XNamespace Tns = "https://servicos.portaldasfinancas.gov.pt/sgdtws/documentosTransporte/";

    private readonly AtOptions _options = options.Value;

    public async Task<AtTransportDocumentResult> CommunicateAsync(
        AtTransportDocumentRequest request,
        AtCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credentials);

        if (string.IsNullOrWhiteSpace(_options.PublicKeyPem))
            throw new InvalidOperationException("AT:PublicKeyPem is not configured.");

        var header = WsSecurityHeaderBuilder.Build(
            $"{credentials.TaxRegistrationNumber}/{credentials.SubUserId}",
            credentials.Password,
            _options.PublicKeyPem,
            timeProvider.GetUtcNow().UtcDateTime);

        var envelope = BuildEnvelope(header, request);
        var envelopeXml = envelope.ToString(SaveOptions.DisableFormatting);

        // Temporary: lets you paste the exact request into AT's "Testar Webservice" page to isolate
        // whether a rejection is the envelope/crypto or something else. Remove once confirmed working.
        logger.LogInformation("AT transport document request body: {Body}", envelopeXml);

        using var content = new StringContent(envelopeXml, Encoding.UTF8, "text/xml");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, string.Empty) { Content = content };
        httpRequest.Headers.TryAddWithoutValidation("SOAPAction", $"\"{Tns.NamespaceName}\"");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogInformation("AT transport document response body: {Body}", responseBody);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "AT transport documents webservice returned HTTP {StatusCode} for document {DocumentNumber}.",
                response.StatusCode, request.DocumentNumber);
        }

        return ParseResponse(responseBody);
    }

    private static XDocument BuildEnvelope(WsSecurityHeader header, AtTransportDocumentRequest request)
    {
        var security =
            new XElement(WsseNs + "Security",
                new XElement(WsseNs + "UsernameToken",
                    new XElement(WsseNs + "Username", header.Username),
                    new XElement(WsseNs + "Password", header.Password),
                    new XElement(WsseNs + "Nonce", header.Nonce),
                    new XElement(WsseNs + "Created", header.Created)));

        var body = new XElement(Tns + "envioDocumentoTransporteRequestElem", BuildBodyFields(request));

        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(SoapNs + "Envelope",
                new XElement(SoapNs + "Header", security),
                new XElement(SoapNs + "Body", body)));
    }

    /// <summary>
    /// Field order matches the WSDL's <c>StockMovement</c> sequence exactly — document-style SOAP is
    /// order-sensitive, unlike a JSON body.
    /// </summary>
    private static IEnumerable<XElement> BuildBodyFields(AtTransportDocumentRequest r)
    {
        yield return new XElement("TaxRegistrationNumber", r.IssuerTaxId);
        yield return new XElement("CompanyName", r.CompanyName);
        yield return BuildAddress("CompanyAddress", r.CompanyAddress);
        yield return new XElement("DocumentNumber", r.DocumentNumber);

        if (!string.IsNullOrWhiteSpace(r.Atcud))
            yield return new XElement("ATCUD", r.Atcud);

        yield return new XElement("MovementStatus", r.MovementStatus);
        yield return new XElement("MovementDate", r.MovementDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        yield return new XElement("MovementType", r.MovementType);

        // xsd:choice: AT rejects the call if both are present.
        yield return r.PartyIsSupplier
            ? new XElement("SupplierTaxID", r.PartyTaxId)
            : new XElement("CustomerTaxID", r.PartyTaxId);

        if (!string.IsNullOrWhiteSpace(r.PartyName))
            yield return new XElement("CustomerName", r.PartyName);

        if (r.ShipTo is not null)
            yield return BuildAddress("AddressTo", r.ShipTo);

        yield return BuildAddress("AddressFrom", r.ShipFrom);

        if (r.MovementEndAtUtc is { } end)
            yield return new XElement("MovementEndTime", FormatDateTime(end));

        yield return new XElement("MovementStartTime", FormatDateTime(r.MovementStartAtUtc));

        if (!string.IsNullOrWhiteSpace(r.VehiclePlate))
            yield return new XElement("VehicleID", r.VehiclePlate);

        foreach (var line in r.Lines)
        {
            yield return new XElement("Line",
                new XElement("ProductDescription", line.ProductDescription),
                new XElement("Quantity", line.Quantity.ToString(CultureInfo.InvariantCulture)),
                new XElement("UnitOfMeasure", line.UnitOfMeasure),
                new XElement("UnitPrice", line.UnitPrice.ToString(CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>AddressStructurePT: country is fixed to "PT" by the schema itself.</summary>
    private static XElement BuildAddress(string elementName, AtTransportDocumentAddress address)
    {
        var element = new XElement(elementName);

        if (!string.IsNullOrWhiteSpace(address.AddressDetail))
            element.Add(new XElement("Addressdetail", address.AddressDetail));

        if (!string.IsNullOrWhiteSpace(address.City))
            element.Add(new XElement("City", address.City));

        if (!string.IsNullOrWhiteSpace(address.PostalCode))
            element.Add(new XElement("PostalCode", address.PostalCode));

        element.Add(new XElement("Country", "PT"));

        return element;
    }

    private static string FormatDateTime(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static AtTransportDocumentResult ParseResponse(string xml)
    {
        var document = XDocument.Parse(xml);

        var fault = document.Descendants(SoapNs + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultString = fault.Element("faultstring")?.Value ?? fault.ToString();
            throw new AtTransportDocumentException(-1, faultString);
        }

        var responseElement = document.Descendants(Tns + "envioDocumentoTransporteResponseElem").FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Unexpected AT response: no envioDocumentoTransporteResponseElem element found.");

        var status = responseElement.Element("ResponseStatus")
            ?? throw new InvalidOperationException("Unexpected AT response: no ResponseStatus element found.");

        var returnCode = int.Parse(status.Element("ReturnCode")?.Value ?? "0", CultureInfo.InvariantCulture);
        var returnMessage = status.Element("ReturnMessage")?.Value;

        // The manual documents -100 as an alert, not an error: the document was still accepted.
        var isAlert = returnCode == -100;
        var success = returnCode == 0 || isAlert;

        if (!success)
            throw new AtTransportDocumentException(returnCode, returnMessage);

        return new AtTransportDocumentResult(
            Success: true,
            IsAlert: isAlert,
            ReturnCode: returnCode,
            ReturnMessage: returnMessage,
            AtDocCodeId: responseElement.Element("ATDocCodeID")?.Value,
            DocumentNumber: responseElement.Element("DocumentNumber")?.Value,
            Atcud: responseElement.Element("ATCUD")?.Value);
    }
}
