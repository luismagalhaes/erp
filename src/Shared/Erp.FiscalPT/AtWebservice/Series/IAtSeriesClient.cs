namespace Erp.FiscalPT.AtWebservice.Series;

/// <summary>The three mutating operations of AT's "Comunicação de Séries Documentais" webservice.</summary>
public interface IAtSeriesClient
{
    Task<AtSeriesOperationResult> RegisterAsync(
        AtSeriesRegistrationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default);

    Task<AtSeriesOperationResult> CancelAsync(
        AtSeriesCancellationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default);

    Task<AtSeriesOperationResult> FinalizeAsync(
        AtSeriesFinalizationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default);
}
