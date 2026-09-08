using Erp.Inventory.Infrastructure.Application;
using Erp.FiscalPT;
using Erp.FiscalPT.Signing;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using NSubstitute;
using Erp.Common;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.Sales.Tests;

/// <summary>
/// Wires the issuing service with substituted storages so the tests exercise the domain rules
/// without a database. The signer is substituted too: signature formatting has its own tests
/// in Erp.FiscalPT.Tests.
/// </summary>
internal sealed class SalesTestContext
{
    public ISalesDocumentStorage DocumentStorage { get; } = Substitute.For<ISalesDocumentStorage>();
    public IStockMovementStorage MovementStorage { get; } = Substitute.For<IStockMovementStorage>();
    public ISeriesStorage SeriesStorage { get; } = Substitute.For<ISeriesStorage>();
    public IErpUnitOfWork UnitOfWork { get; } = Substitute.For<IErpUnitOfWork>();
    public IDocumentSigner Signer { get; } = Substitute.For<IDocumentSigner>();
    public IErpTransaction Transaction { get; } = Substitute.For<IErpTransaction>();

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

    /// <summary>
    /// Substituted: what the recorder does with the stock has its own tests in Erp.Inventory.Tests.
    /// Here it only matters that issuing calls it.
    /// </summary>
    public IStockRecorder StockRecorder { get; } = Substitute.For<IStockRecorder>();

    public SalesDocumentService CreateService() =>
        new(DocumentStorage, SeriesStorage, MovementStorage, UnitOfWork, Signer, StockRecorder, Options.Create(Fiscal));

    /// <summary>A series ready to issue, already carrying a validation code from the tax authority.</summary>
    /// <param name="stockEffect">
    /// Defaults to moving no stock, so the tests that are not about stock stay unaffected.
    /// </param>
    public Series GivenCommunicatedSeries(
        Guid companyId,
        string seriesCode = "A2026",
        string documentType = "FT",
        StockEffect stockEffect = StockEffect.None)
    {
        var series = new Series
        {
            CompanyId = companyId,
            DocumentType = documentType,
            SeriesCode = seriesCode,
            StockEffect = stockEffect
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);

        SeriesStorage.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);
        SeriesStorage.GetByIdAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        return series;
    }

    /// <summary>
    /// Serves a delivery note line the service can resolve, so a request naming an origin gets
    /// past the validation and reaches the recorder.
    /// </summary>
    public void GivenInvoiceableMovementLine(Guid companyId, Guid lineId, decimal quantity = 100m)
    {
        var series = new Series { CompanyId = companyId, DocumentType = "GR", SeriesCode = "G2026" };
        series.Communicate("JFTX7RK9", DateTime.UtcNow);

        var line = new StockMovementLine
        {
            Id = lineId,
            LineNumber = 1,
            ProductCode = "ART001",
            ProductDescription = "Artigo de teste",
            Quantity = quantity,
            UnitPrice = 100m
        };

        var location = new MovementLocation("Rua da Fábrica");

        var movement = StockMovement.Issue(
            companyId, series, series.TakeNextSequence(), "GR G2026/1", "JFTX7RK9-1",
            new DateOnly(2026, 1, 10), new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            new MovementParty("500123456", "Cliente Teste"), location, location,
            new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), null, null, null,
            [line], quantity * 100m, 0m, quantity * 100m,
            new string('x', 44), string.Empty, "1", "user-1");

        MovementStorage
            .GetForUpdateByLinesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<StockMovement>)[movement]);
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
