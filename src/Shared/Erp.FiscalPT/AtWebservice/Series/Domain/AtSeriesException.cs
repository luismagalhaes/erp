namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>
/// The AT series webservice answered with a return code other than the operation's success code.
/// Carries AT's own code and message forward instead of reinterpreting them.
/// </summary>
public sealed class AtSeriesException(int returnCode, string? returnMessage)
    : Exception($"AT rejected the series operation (code {returnCode}): {returnMessage ?? "no message"}")
{
    public int ReturnCode { get; } = returnCode;

    public string? ReturnMessage { get; } = returnMessage;
}
