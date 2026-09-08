using System.Net.Http.Json;
using Erp.Common;
using Erp.Main.Models.FiscalPT;

namespace Erp.Main.Services;

/// <summary>
/// SAF-T (PT) exports. The file is cross module: the exporter collects from every module that
/// registers a document source, so this client does not belong to Sales.
/// </summary>
public class SaftApiClient(HttpClient http) : ApiClientBase(http)
{
    public async Task<(SaftPeriodSummary? Summary, string? Error)> GetSaftSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft/summary?companyId={companyId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        return (await response.Content.ReadFromJsonAsync<SaftPeriodSummary>(cancellationToken), null);
    }

    /// <summary>
    /// Fetches the generated file. The name comes from the API, because it is the tax authority's
    /// naming convention and belongs with the code that builds the file.
    /// </summary>
    public async Task<(SaftFile? File, string? Error)> DownloadSaftAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft?companyId={companyId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"SAFT_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xml";

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return (new SaftFile(fileName, content, ReadValidationErrors(response)), null);
    }

    /// <summary>
    /// The self-billing file, of type "S". It comes out one per supplier, with the supplier's tax id
    /// in the header, because the invoices in it are their sales and not ours.
    /// </summary>
    public async Task<(SaftFile? File, string? Error)> DownloadSelfBillingSaftAsync(
        Guid companyId,
        Guid supplierId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var response = await Http.GetAsync(
            $"api/saft/self-billing?companyId={companyId}&supplierId={supplierId}" +
            $"&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (null, await ApiResponse.ReadErrorAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
            ?? $"SAFT_S_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xml";

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return (new SaftFile(fileName, content, ReadValidationErrors(response)), null);
    }

    private static int ReadValidationErrors(HttpResponseMessage response) =>
        response.Headers.TryGetValues(Constants.Headers.SaftValidationErrors, out var values)
        && int.TryParse(values.FirstOrDefault(), out var count)
            ? count
            : 0;
}
