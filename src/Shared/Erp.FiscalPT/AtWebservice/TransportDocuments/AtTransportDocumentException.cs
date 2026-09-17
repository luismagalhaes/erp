namespace Erp.FiscalPT.AtWebservice.TransportDocuments;

/// <summary>
/// The AT webservice answered with a return code other than success (0) or the documented alert
/// (-100). Carries AT's own code and message forward instead of reinterpreting them.
/// </summary>
public sealed class AtTransportDocumentException(int returnCode, string? returnMessage)
    : Exception($"AT rejected the transport document (code {returnCode}): {returnMessage ?? "no message"}")
{
    public int ReturnCode { get; } = returnCode;

    public string? ReturnMessage { get; } = returnMessage;
}
