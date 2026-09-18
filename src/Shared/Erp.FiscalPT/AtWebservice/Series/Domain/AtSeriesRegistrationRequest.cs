namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>Fields for AT's <c>registarSerie</c> operation.</summary>
public sealed record AtSeriesRegistrationRequest(
    string Serie,
    string TipoSerie,
    string ClasseDoc,
    string TipoDoc,
    int NumInicialSeq,
    DateOnly DataInicioPrevUtiliz,
    int NumCertSWFatur,
    string MeioProcessamento);
