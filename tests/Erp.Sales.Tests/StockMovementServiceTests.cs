using Erp.SeriesRegistry.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.FiscalPT;
using Erp.FiscalPT.AtWebservice;
using Erp.FiscalPT.AtWebservice.TransportDocuments;
using Erp.FiscalPT.Signing;
using Erp.Sales.Application.Services;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Erp.Common;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.Sales.Tests;

public class StockMovementServiceTests
{
    private readonly IStockMovementStorage _movements = Substitute.For<IStockMovementStorage>();
    private readonly ISeriesStorage _series = Substitute.For<ISeriesStorage>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDocumentSigner _signer = Substitute.For<IDocumentSigner>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();
    private readonly IAtTransportDocumentClient _atClient = Substitute.For<IAtTransportDocumentClient>();
    private readonly IAtCompanyProfileProvider _atProfiles = Substitute.For<IAtCompanyProfileProvider>();
    private readonly List<StockMovement> _persisted = [];
    private readonly List<MovementStatusChange> _persistedStatusChanges = [];
    private readonly Guid _companyId = Guid.NewGuid();

    public StockMovementServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);
        _movements.GetLastHashAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(string.Empty);

        _signer.Sign(Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>())
            .Returns(new DocumentSignature(new string('x', 44), "1"));

        _movements.When(x => x.AddAsync(Arg.Any<StockMovement>(), Arg.Any<CancellationToken>()))
            .Do(call => _persisted.Add(call.Arg<StockMovement>()));

        _movements.When(x => x.AddStatusChangeAsync(Arg.Any<MovementStatusChange>(), Arg.Any<CancellationToken>()))
            .Do(call => _persistedStatusChanges.Add(call.Arg<MovementStatusChange>()));

        _atProfiles.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new AtCompanyProfile("123456789", "Acme", "Rua A", "Lisboa", "1000-000", "1", "secret"));

        _atClient.CommunicateAsync(Arg.Any<AtTransportDocumentRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new AtTransportDocumentResult(true, false, 0, null, "ABC123", null, null));
    }

    /// <summary>
    /// Substituted: what the recorder does with the stock has its own tests in Erp.Inventory.Tests.
    /// </summary>
    private readonly IStockRecorder _stockRecorder = Substitute.For<IStockRecorder>();

    private StockMovementService CreateService() =>
        new(_movements, _series, _unitOfWork, _signer, _stockRecorder, _atClient, _atProfiles,
            NullLogger<StockMovementService>.Instance,
            Options.Create(new FiscalOptions { IssuerTaxId = "123456789", CertificateNumber = "9999", KeyVersion = "1" }));

    private Series GivenSeries(string documentType = "GT", Guid? companyId = null)
    {
        var series = new Series
        {
            CompanyId = companyId ?? _companyId,
            DocumentType = documentType,
            SeriesCode = "A2026"
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        _series.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        return series;
    }

    private static CreateStockMovementRequest Request(
        Guid companyId,
        Guid seriesId,
        string shipFrom = "Rua da Fábrica 1, Porto",
        string shipTo = "Rua do Cliente 2, Lisboa",
        params CreateStockMovementLineRequest[] lines) =>
        new(companyId,
            seriesId,
            new DateOnly(2026, 3, 10),
            new MovementPartyRequest("500123456", "Cliente Teste"),
            new MovementLocationRequest(shipFrom, "Porto", "4000-001"),
            new MovementLocationRequest(shipTo, "Lisboa", "1000-001"),
            new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc),
            lines.Length == 0 ? [Line()] : lines,
            VehiclePlate: "AA-00-BB");

    private static CreateStockMovementLineRequest Line(
        decimal quantity = 3,
        decimal unitPrice = 50m,
        string taxCode = "NOR",
        decimal taxPercentage = 23m,
        string? exemptionReason = null) =>
        new("ART001", "Artigo de teste", quantity, unitPrice, taxCode, taxPercentage,
            TaxExemptionReason: exemptionReason);

    [Fact]
    public async Task IssueAsync_numbers_the_movement_from_the_series()
    {
        var series = GivenSeries();

        var issued = await CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        issued.DocumentNumber.Should().Be("GT A2026/1");
        issued.Atcud.Should().Be("JFTX7RK9-1");
        issued.MovementType.Should().Be("GT");
        series.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_totals_the_quantity_and_the_amounts()
    {
        var series = GivenSeries();

        var issued = await CreateService().IssueAsync(
            Request(_companyId, series.Id, lines: [Line(quantity: 3, unitPrice: 50m), Line(quantity: 2, unitPrice: 10m)]),
            "user-1");

        issued.TotalQuantity.Should().Be(5m);
        issued.NetTotal.Should().Be(170.00m);
        issued.TaxPayable.Should().Be(39.10m);
        issued.GrossTotal.Should().Be(209.10m);
    }

    [Fact]
    public async Task IssueAsync_keeps_the_loading_and_delivery_points()
    {
        var series = GivenSeries();

        var issued = await CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        issued.ShipFrom.Address.Should().Be("Rua da Fábrica 1, Porto");
        issued.ShipFrom.City.Should().Be("Porto");
        issued.ShipTo.Address.Should().Be("Rua do Cliente 2, Lisboa");
        issued.VehiclePlate.Should().Be("AA-00-BB");
    }

    [Fact]
    public async Task IssueAsync_chains_the_movement_to_the_previous_hash_of_the_series()
    {
        var series = GivenSeries();
        _movements.GetLastHashAsync(series.Id, Arg.Any<CancellationToken>()).Returns("previous-hash");

        await CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        _signer.Received(1).Sign(
            Arg.Any<DateOnly>(), Arg.Any<DateTime>(), "GT A2026/1", Arg.Any<decimal>(), "previous-hash");
        _persisted.Single().PreviousHash.Should().Be("previous-hash");
    }

    [Fact]
    public async Task IssueAsync_locks_the_series_row_and_commits_once()
    {
        var series = GivenSeries();

        await CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        await _series.Received(1).GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>());
        await _transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_refuses_an_invoicing_series()
    {
        var series = GivenSeries(documentType: "FT");

        var act = () => CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not for goods movements*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_without_a_validation_code()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "GT", SeriesCode = "A2026" };
        _series.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var act = () => CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*validation code*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_from_another_company()
    {
        var series = GivenSeries(companyId: Guid.NewGuid());

        var act = () => CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("", "Rua do Cliente 2")]
    [InlineData("Rua da Fábrica 1", "")]
    public async Task IssueAsync_requires_both_transport_addresses(string shipFrom, string shipTo)
    {
        var series = GivenSeries();

        var act = () => CreateService().IssueAsync(
            Request(_companyId, series.Id, shipFrom, shipTo), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*address*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_movement_without_lines()
    {
        var series = GivenSeries();
        var request = Request(_companyId, series.Id) with { Lines = [] };

        var act = () => CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*at least one line*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_line_without_a_positive_quantity()
    {
        var series = GivenSeries();

        var act = () => CreateService().IssueAsync(
            Request(_companyId, series.Id, lines: [Line(quantity: 0)]), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*positive quantity*");
    }

    [Fact]
    public async Task IssueAsync_leaves_the_document_waiting_for_the_tax_authority_code()
    {
        var series = GivenSeries();

        var issued = await CreateService().IssueAsync(Request(_companyId, series.Id), "user-1");

        issued.AtDocCodeId.Should().BeNull("the goods cannot move before the document is communicated");
        issued.CommunicatedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task CommunicateAsync_records_the_code_the_tax_authority_returned()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        var communicated = await service.CommunicateAsync(issued.Id);

        communicated!.AtDocCodeId.Should().Be("ABC123");
        communicated.CommunicatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task CommunicateAsync_accepts_the_documented_alert_as_success()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        _atClient.CommunicateAsync(Arg.Any<AtTransportDocumentRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(new AtTransportDocumentResult(true, true, -100, "late communication", "ABC123", null, null));

        var communicated = await service.CommunicateAsync(issued.Id);

        communicated!.AtDocCodeId.Should().Be("ABC123");
    }

    [Fact]
    public async Task CommunicateAsync_surfaces_an_at_rejection()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        _atClient.CommunicateAsync(Arg.Any<AtTransportDocumentRequest>(), Arg.Any<AtCredentials>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AtTransportDocumentResult>(new AtTransportDocumentException(-7, "NIF mismatch")));

        var act = () => service.CommunicateAsync(issued.Id);

        await act.Should().ThrowAsync<AtTransportDocumentException>();
        movement.AtDocCodeId.Should().BeNull();
    }

    [Fact]
    public async Task CommunicateAsync_requires_the_company_to_have_at_credentials_configured()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        _atProfiles.GetAsync(_companyId, Arg.Any<CancellationToken>()).Returns((AtCompanyProfile?)null);

        var act = () => service.CommunicateAsync(issued.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*AT WDT credentials*");
    }

    [Fact]
    public async Task CommunicateAsync_refuses_to_communicate_twice()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        await service.CommunicateAsync(issued.Id);

        var act = () => service.CommunicateAsync(issued.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already communicated*");
    }

    [Fact]
    public async Task VoidAsync_writes_a_status_change_without_touching_the_header()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(_companyId, series.Id), "user-1");

        var movement = _persisted.Single();
        _movements.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(movement);

        var voided = await service.VoidAsync(issued.Id, "Transporte cancelado", "user-2");

        voided!.Status.Should().Be("A");
        movement.Status.Should().Be("N", "the header row is never updated");
        _persistedStatusChanges.Single().Reason.Should().Be("Transporte cancelado");
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_an_unknown_movement()
    {
        var result = await CreateService().VoidAsync(Guid.NewGuid(), "Motivo", "user-1");

        result.Should().BeNull();
    }
}
