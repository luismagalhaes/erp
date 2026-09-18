namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>Fields for AT's <c>finalizarSerie</c> operation.</summary>
public sealed record AtSeriesFinalizationRequest(
    string Serie,
    string ClasseDoc,
    string TipoDoc,
    string CodValidacaoSerie,
    int SeqUltimoDocEmitido,
    string? Justificacao);
