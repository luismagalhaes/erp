using Erp.Sales.Application.Configuration;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Sales.Tests;

/// <summary>
/// Wires the issuing service with substituted storages so the tests exercise the domain rules
/// without a database. The signer is substituted too: signature formatting has its own tests
/// in Erp.FiscalPT.Tests.
/// </summary>
internal sealed class SalesTestContext
{
    public ISalesDocumentStorage DocumentStorage { get; } = Substitute.For<ISalesDocumentStorage>();
    public ISeriesStorage SeriesStorage { get; } = Substitute.For<ISeriesStorage>();
    public ISalesUnitOfWork UnitOfWork { get; } = Substitute.For<ISalesUnitOfWork>();
    public IDocumentSigner Signer { get; } = Substitute.For<IDocumentSigner>();
    public ISalesTransaction Transaction { get; } = Substitute.For<ISalesTransaction>();

    public List<SalesDocument> Persisted { get; } = [];
    public List<DocumentStatusChange> PersistedStatusChanges { get; } = [];

    public FiscalOptions Fiscal { get; } = new()
    {
        IssuerTaxId = "123456789",
        CertificateNumber = "9999",
        KeyVersion = "1"
    };

    public SalesTestContext()
    {
        UnitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Transaction);

        DocumentStorage.GetLastHashAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        // A 44 character hash is enough for the printable characters at positions 1, 11, 21 and 31.
        Signer.Sign(
                Arg.Any<DateOnly>(),
                Arg.Any<DateTime>(),
                Arg.Any<string>(),
                Arg.Any<decimal>(),
                Arg.Any<string>())
            .Returns(new DocumentSignature(new string('x', 10) + new string('y', 10) + new string('z', 24), "1"));

        DocumentStorage
            .When(x => x.AddAsync(Arg.Any<SalesDocument>(), Arg.Any<CancellationToken>()))
            .Do(call => Persisted.Add(call.Arg<SalesDocument>()));

        DocumentStorage
            .When(x => x.AddStatusChangeAsync(Arg.Any<DocumentStatusChange>(), Arg.Any<CancellationToken>()))
            .Do(call => PersistedStatusChanges.Add(call.Arg<DocumentStatusChange>()));
    }

    public SalesDocumentService CreateService() =>
        new(DocumentStorage, SeriesStorage, UnitOfWork, Signer, Options.Create(Fiscal));

    /// <summary>A series ready to issue, already carrying a validation code from the tax authority.</summary>
    public Series GivenCommunicatedSeries(Guid companyId, string seriesCode = "A2026", string documentType = "FT")
    {
        var series = new Series
        {
            CompanyId = companyId,
            DocumentType = documentType,
            SeriesCode = seriesCode
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);

        SeriesStorage.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);
        SeriesStorage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        return series;
    }

    public static CreateInvoiceRequest InvoiceRequest(
        Guid companyId,
        Guid seriesId,
        params CreateInvoiceLineRequest[] lines)
    {
        return new CreateInvoiceRequest(
            companyId,
            seriesId,
            new DateOnly(2026, 1, 15),
            new CustomerRequest("500123456", "Cliente Teste", "Rua Um, Lisboa"),
            lines.Length == 0 ? [Line()] : lines);
    }

    public static CreateInvoiceLineRequest Line(
        decimal quantity = 2,
        decimal unitPrice = 100m,
        string taxCode = "NOR",
        decimal taxPercentage = 23m,
        string? exemptionReason = null)
    {
        return new CreateInvoiceLineRequest(
            "ART001",
            "Artigo de teste",
            quantity,
            unitPrice,
            taxCode,
            taxPercentage,
            TaxExemptionReason: exemptionReason);
    }
}
