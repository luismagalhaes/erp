namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>
/// Fields for AT's <c>anularSerie</c> operation. <c>DeclaracaoNaoEmissao</c> attests that no
/// document was issued with this series — only send <c>true</c> when that is actually the case.
/// </summary>
public sealed record AtSeriesCancellationRequest(
    string Serie,
    string ClasseDoc,
    string TipoDoc,
    string CodValidacaoSerie,
    string Motivo,
    bool DeclaracaoNaoEmissao);
