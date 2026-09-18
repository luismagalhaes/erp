namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>
/// The AT response, already classified: <see cref="ReturnCode"/> 0 is a plain success, -100 is the
/// documented alert ("mera comunicação de dados", not an error) and is treated as success too, and
/// anything else is a failure that carries the code and AT's own message forward untranslated.
/// </summary>
public sealed record AtTransportDocumentResult(
    bool Success,
    bool IsAlert,
    int ReturnCode,
    string? ReturnMessage,
    string? AtDocCodeId,
    string? DocumentNumber,
    string? Atcud);
