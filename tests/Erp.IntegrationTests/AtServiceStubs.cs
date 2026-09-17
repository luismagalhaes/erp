using Erp.FiscalPT.AtWebservice;
using Erp.FiscalPT.AtWebservice.Series;
using Erp.FiscalPT.AtWebservice.TransportDocuments;

namespace Erp.IntegrationTests;

/// <summary>
/// Stands in for the real AT webservice clients in the integration container: these tests exercise
/// the ERP's own wiring (storage, locking, domain rules), not a live SOAP call to the tax authority.
/// Registered in <see cref="SqlServerFixture"/>, after <c>AddModules</c>, so it overrides the real
/// HTTP-backed clients that library registers.
/// </summary>
internal sealed class StubAtSeriesClient : IAtSeriesClient
{
    public Task<AtSeriesOperationResult> RegisterAsync(
        AtSeriesRegistrationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AtSeriesOperationResult(2001, null, "JFTX7RK9", "A"));

    public Task<AtSeriesOperationResult> CancelAsync(
        AtSeriesCancellationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AtSeriesOperationResult(2003, null, null, "N"));

    public Task<AtSeriesOperationResult> FinalizeAsync(
        AtSeriesFinalizationRequest request, AtCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AtSeriesOperationResult(2004, null, null, "F"));
}

internal sealed class StubAtTransportDocumentClient : IAtTransportDocumentClient
{
    public Task<AtTransportDocumentResult> CommunicateAsync(
        AtTransportDocumentRequest request, AtCredentials credentials, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AtTransportDocumentResult(true, false, 0, null, "ABC123", request.DocumentNumber, request.Atcud));
}

internal sealed class StubAtCompanyProfileProvider : IAtCompanyProfileProvider
{
    public Task<AtCompanyProfile?> GetAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        Task.FromResult<AtCompanyProfile?>(new AtCompanyProfile(
            TaxId: "500123456",
            CompanyName: "Empresa de Teste",
            Address: "Rua Um",
            City: "Lisboa",
            PostalCode: "1000-001",
            SubUserId: "1",
            Password: "test-password"));
}
