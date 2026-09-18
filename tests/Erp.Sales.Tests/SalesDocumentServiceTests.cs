using Erp.SeriesRegistry.Domain;
using Erp.Sales.Domain;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

public class SalesDocumentServiceTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();

    /// <summary>
    /// An invoice must not take its number from a receipt or transport series: that would burn
    /// numbers in a series belonging to another document family.
    /// </summary>
    [Theory]
    [InlineData("GT")]
    [InlineData("RG")]
    public async Task IssueAsync_rejects_a_series_of_another_document_family(string documentType)
    {
        var context = new SalesTestContext();
        var companyId = Guid.NewGuid();
        var series = context.GivenCommunicatedSeries(companyId, documentType: documentType);

        var act = () => context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not for invoicing*");
    }

    [Fact]
    public async Task IssueAsync_numbers_the_document_from_the_series()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        issued.DocumentNumber.Should().Be("FT A2026/1");
        issued.Atcud.Should().Be("JFTX7RK9-1");
        series.CurrentSequence.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_computes_totals_from_the_lines()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(quantity: 2, unitPrice: 100m),
                SalesTestContext.Line(quantity: 1, unitPrice: 50m, taxCode: "RED", taxPercentage: 6m)),
            "user-1");

        issued.NetTotal.Should().Be(250.00m);
        issued.TaxPayable.Should().Be(49.00m);   // 200 * 23% + 50 * 6%
        issued.GrossTotal.Should().Be(299.00m);
    }

    [Fact]
    public async Task IssueAsync_groups_taxes_by_rate_for_the_qr_code()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(quantity: 1, unitPrice: 100m),
                SalesTestContext.Line(quantity: 1, unitPrice: 200m),
                SalesTestContext.Line(quantity: 1, unitPrice: 50m, taxCode: "RED", taxPercentage: 6m)),
            "user-1");

        issued.Taxes.Should().HaveCount(2);
        issued.Taxes.Single(x => x.TaxCode == "NOR").TaxableBase.Should().Be(300m);
        issued.Taxes.Single(x => x.TaxCode == "RED").TaxableBase.Should().Be(50m);
        issued.QrCodePayload.Should().Contain("I7:300.00").And.Contain("I3:50.00");
    }

    [Fact]
    public async Task IssueAsync_chains_the_document_to_the_previous_hash_of_the_series()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        _context.DocumentStorage.GetLastHashAsync(series.Id, Arg.Any<CancellationToken>())
            .Returns("previous-hash");

        var service = _context.CreateService();

        await service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        _context.Signer.Received(1).Sign(
            Arg.Any<DateOnly>(),
            Arg.Any<DateTime>(),
            "FT A2026/1",
            Arg.Any<decimal>(),
            "previous-hash");

        _context.Persisted.Single().PreviousHash.Should().Be("previous-hash");
    }

    [Fact]
    public async Task IssueAsync_signs_the_gross_total_that_is_persisted()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        _context.Signer.Received(1).Sign(
            Arg.Any<DateOnly>(),
            Arg.Any<DateTime>(),
            Arg.Any<string>(),
            issued.GrossTotal,
            Arg.Any<string>());
    }

    [Fact]
    public async Task IssueAsync_records_the_system_entry_date_with_second_precision()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        issued.SystemEntryDateUtc.Millisecond.Should().Be(0);
        issued.SystemEntryDateUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task IssueAsync_commits_the_transaction_once_everything_succeeded()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        await service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await _context.Transaction.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_locks_the_series_row_instead_of_reading_it_normally()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        await service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await _context.SeriesStorage.Received(1).GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>());
        await _context.SeriesStorage.DidNotReceive().GetByIdAsync(series.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_without_a_validation_code()
    {
        var series = new Series { CompanyId = _companyId, DocumentType = "FT", SeriesCode = "A2026" };
        _context.SeriesStorage.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        var service = _context.CreateService();

        var act = () => service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation code*");
        _context.Persisted.Should().BeEmpty();
    }

    /// <summary>
    /// A self-billing series has document type "FT" like any other, so nothing else tells it apart.
    /// Its numbers belong to a supplier's documents and to the "S" SAF-T; one of our own sales in
    /// that chain would end up in the wrong file.
    /// </summary>
    [Fact]
    public async Task IssueAsync_refuses_a_self_billing_series()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        series.SelfBilling = true;

        var service = _context.CreateService();

        var act = () => service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*self-billing*");
        _context.Persisted.Should().BeEmpty();
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_from_another_company()
    {
        var series = _context.GivenCommunicatedSeries(Guid.NewGuid());
        var service = _context.CreateService();

        var act = () => service.IssueAsync(SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IssueAsync_refuses_a_document_without_lines()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with { Lines = [] };

        var act = () => service.IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task IssueAsync_refuses_an_exempt_line_without_a_reason()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var act = () => service.IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(taxCode: "ISE", taxPercentage: 0m)),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exemption code*");
    }

    [Fact]
    public async Task IssueAsync_refuses_an_exemption_code_the_tax_authority_does_not_know()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(taxCode: "ISE", taxPercentage: 0m, exemptionCode: "M03")),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*unknown VAT exemption code 'M03'*");
    }

    /// <summary>The code is what is chosen; the legal basis SAF-T wants comes from the AT's table.</summary>
    [Fact]
    public async Task IssueAsync_writes_the_legal_basis_of_the_exemption_code()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(taxCode: "ISE", taxPercentage: 0m, exemptionCode: "m07")),
            "user-1");

        var line = _context.Persisted.Single().Lines.Single();
        line.TaxExemptionCode.Should().Be("M07");
        line.TaxExemptionReason.Should().Be("Artigo 9.º do CIVA");
    }

    [Fact]
    public async Task IssueAsync_drops_an_exemption_sent_on_a_taxed_line()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(exemptionCode: "M07", exemptionReason: "Artigo 9.º do CIVA")),
            "user-1");

        var line = _context.Persisted.Single().Lines.Single();
        line.TaxExemptionCode.Should().BeNull();
        line.TaxExemptionReason.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_refuses_a_line_without_a_positive_quantity()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var act = () => service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id, SalesTestContext.Line(quantity: 0)),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*positive quantity*");
    }

    [Fact]
    public async Task IssueAsync_does_not_advance_the_series_when_the_document_is_rejected()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var act = () => service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id, SalesTestContext.Line(quantity: -1)),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>();
        series.CurrentSequence.Should().Be(0);
    }

    [Fact]
    public async Task VoidAsync_writes_a_status_change_without_touching_the_document_header()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        var document = _context.Persisted.Single();
        _context.DocumentStorage.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(document);

        var voided = await service.VoidAsync(issued.Id, "Erro de faturação", "user-2");

        voided!.Status.Should().Be("A");
        document.Status.Should().Be("N", "the header row is never updated");
        _context.PersistedStatusChanges.Single().Reason.Should().Be("Erro de faturação");
        _context.PersistedStatusChanges.Single().UserId.Should().Be("user-2");
    }

    [Fact]
    public async Task VoidAsync_refuses_to_void_the_same_document_twice()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var service = _context.CreateService();

        var issued = await service.IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        var document = _context.Persisted.Single();
        _context.DocumentStorage.GetByIdAsync(issued.Id, Arg.Any<CancellationToken>()).Returns(document);

        await service.VoidAsync(issued.Id, "Primeiro motivo", "user-2");

        var act = () => service.VoidAsync(issued.Id, "Segundo motivo", "user-2");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already voided*");
    }

    [Fact]
    public async Task VoidAsync_returns_null_for_an_unknown_document()
    {
        var service = _context.CreateService();

        var result = await service.VoidAsync(Guid.NewGuid(), "Motivo", "user-1");

        result.Should().BeNull();
    }
}
