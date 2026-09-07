using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

/// <summary>
/// Invoicing a delivery note. A movement may be invoiced across several documents, but never for
/// more than it moved, and the invoice line keeps the reference the SAF-T needs.
/// </summary>
public class InvoiceFromMovementTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Dictionary<Guid, decimal> _invoiced = [];

    public InvoiceFromMovementTests()
    {
        _context.DocumentStorage
            .GetInvoicedQuantitiesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyDictionary<Guid, decimal>)call
                .Arg<IReadOnlyCollection<Guid>>()
                .Where(_invoiced.ContainsKey)
                .ToDictionary(id => id, id => _invoiced[id]));
    }

    /// <summary>A delivery note of ten units, registered in the substituted movement storage.</summary>
    private StockMovement GivenDeliveryNote(
        decimal quantity = 10m,
        string movementType = "GR",
        Guid? companyId = null)
    {
        var series = new Series
        {
            CompanyId = companyId ?? _companyId,
            DocumentType = movementType,
            SeriesCode = "G2026"
        };

        series.Communicate("JFTX7RK9", DateTime.UtcNow);
        var sequence = series.TakeNextSequence();

        var line = new StockMovementLine
        {
            LineNumber = 1,
            ProductCode = "ART001",
            ProductDescription = "Artigo de teste",
            Quantity = quantity,
            UnitOfMeasure = "UN",
            UnitPrice = 100m,
            LineAmount = quantity * 100m,
            TaxCountryRegion = "PT",
            TaxCode = "NOR",
            TaxPercentage = 23m,
            TaxAmount = quantity * 23m
        };

        var location = new MovementLocation("Rua da Fábrica", "Porto", "4000-001");

        var movement = StockMovement.Issue(
            companyId ?? _companyId,
            series,
            sequence,
            $"{movementType} G2026/{sequence}",
            $"JFTX7RK9-{sequence}",
            new DateOnly(2026, 1, 10),
            new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            new MovementParty("500123456", "Cliente Teste"),
            location,
            location,
            new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc),
            null,
            "AA-00-BB",
            null,
            [line],
            quantity * 100m,
            quantity * 23m,
            quantity * 123m,
            new string('x', 44),
            string.Empty,
            "1",
            "user-1");

        _context.MovementStorage
            .GetForUpdateByLinesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<StockMovement>)[movement]);

        return movement;
    }

    private CreateInvoiceRequest RequestFrom(Guid seriesId, StockMovement movement, decimal quantity) =>
        new(_companyId,
            seriesId,
            new DateOnly(2026, 1, 15),
            new CustomerRequest("500123456", "Cliente Teste", null),
            [
                new CreateInvoiceLineRequest(
                    "ART001", "Artigo de teste", quantity, 100m, "NOR", 23m,
                    OriginatingLineId: movement.Lines.First().Id)
            ]);

    [Fact]
    public async Task IssueAsync_copies_the_delivery_note_reference_onto_the_line()
    {
        var movement = GivenDeliveryNote();
        var series = _context.GivenCommunicatedSeries(_companyId);

        var invoice = await _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 10m), "user-1");

        var line = invoice.Lines.Should().ContainSingle().Subject;
        line.OriginatingNumber.Should().Be(movement.DocumentNumber);
        line.OriginatingDate.Should().Be(movement.MovementDate);

        _context.Persisted[^1].Lines.First().OriginatingLineId.Should().Be(movement.Lines.First().Id);
    }

    [Fact]
    public async Task IssueAsync_allows_invoicing_a_delivery_note_in_parts()
    {
        var movement = GivenDeliveryNote();
        var series = _context.GivenCommunicatedSeries(_companyId);

        var invoice = await _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 4m), "user-1");

        invoice.Lines.Should().ContainSingle().Which.Quantity.Should().Be(4m);
    }

    [Fact]
    public async Task IssueAsync_refuses_to_invoice_more_than_the_movement_carried()
    {
        var movement = GivenDeliveryNote();
        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 11m), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*left to invoice*");
    }

    /// <summary>What earlier invoices already took is off the table.</summary>
    [Fact]
    public async Task IssueAsync_counts_what_is_already_invoiced()
    {
        var movement = GivenDeliveryNote();
        _invoiced[movement.Lines.First().Id] = 7m;

        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 4m), "user-1");

        // Ten moved, seven invoiced, so only three are left.
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*only has 3 left to invoice*");
    }

    [Fact]
    public async Task IssueAsync_allows_what_fits_the_remainder()
    {
        var movement = GivenDeliveryNote();
        _invoiced[movement.Lines.First().Id] = 7m;

        var series = _context.GivenCommunicatedSeries(_companyId);

        var invoice = await _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 3m), "user-1");

        invoice.Lines.Should().ContainSingle().Which.Quantity.Should().Be(3m);
    }

    [Fact]
    public async Task IssueAsync_refuses_a_voided_delivery_note()
    {
        var movement = GivenDeliveryNote();
        movement.Void("Erro", "user-1", DateTime.UtcNow);

        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 1m), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*voided, so it cannot be invoiced*");
    }

    /// <summary>
    /// A guia de ativos próprios moves the entity's own goods and a guia de devolução brings goods
    /// back, so neither is a sale waiting to be invoiced.
    /// </summary>
    [Theory]
    [InlineData("GA")]
    [InlineData("GD")]
    public async Task IssueAsync_refuses_a_movement_that_is_never_invoiced(string movementType)
    {
        var movement = GivenDeliveryNote(movementType: movementType);
        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 1m), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*never invoiced*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_delivery_note_of_another_company()
    {
        var movement = GivenDeliveryNote(companyId: Guid.NewGuid());
        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            RequestFrom(series.Id, movement, 1m), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*belongs to another company*");
    }

    [Fact]
    public async Task IssueAsync_refuses_the_same_movement_line_twice_on_one_invoice()
    {
        var movement = GivenDeliveryNote();
        var series = _context.GivenCommunicatedSeries(_companyId);
        var lineId = movement.Lines.First().Id;

        var request = new CreateInvoiceRequest(
            _companyId,
            series.Id,
            new DateOnly(2026, 1, 15),
            new CustomerRequest("500123456", "Cliente Teste", null),
            [
                new CreateInvoiceLineRequest("ART001", "Artigo", 2m, 100m, "NOR", 23m, OriginatingLineId: lineId),
                new CreateInvoiceLineRequest("ART001", "Artigo", 3m, 100m, "NOR", 23m, OriginatingLineId: lineId)
            ]);

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot be invoiced twice*");
    }

    /// <summary>
    /// The ceiling only holds if the movement is locked before the invoiced quantity is read.
    /// </summary>
    [Fact]
    public async Task IssueAsync_locks_the_movement_before_reading_what_is_already_invoiced()
    {
        var movement = GivenDeliveryNote();
        var series = _context.GivenCommunicatedSeries(_companyId);

        await _context.CreateService().IssueAsync(RequestFrom(series.Id, movement, 1m), "user-1");

        Received.InOrder(() =>
        {
            _context.MovementStorage.GetForUpdateByLinesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
            _context.DocumentStorage.GetInvoicedQuantitiesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task IssueAsync_leaves_an_ordinary_invoice_without_any_reference()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var invoice = await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        invoice.Lines.Should().ContainSingle().Which.OriginatingNumber.Should().BeNull();
    }
}
