namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>
/// The three <c>SeriesWSService</c> mutating operations (registar/anular/finalizar) all answer with
/// the same shape — <c>infoSerie</c> (optional) plus <c>infoResultOper</c> — so one result type
/// covers them all.
/// </summary>
public sealed record AtSeriesOperationResult(
    int ReturnCode,
    string? ReturnMessage,
    string? ValidationCode,
    string? Estado);
