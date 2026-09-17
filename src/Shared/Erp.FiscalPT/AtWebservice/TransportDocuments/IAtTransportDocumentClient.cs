namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>Sends one transport document to the AT "Comunicação dos Documentos de Transporte" webservice.</summary>
public interface IAtTransportDocumentClient
{
    Task<AtTransportDocumentResult> CommunicateAsync(
        AtTransportDocumentRequest request,
        AtCredentials credentials,
        CancellationToken cancellationToken = default);
}
