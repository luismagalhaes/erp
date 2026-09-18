using Erp.Common;
using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.Signing;
using Erp.Purchasing.Application.Services;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.SeriesRegistry.Domain;
using Erp.SeriesRegistry.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Erp.Purchasing.Tests;

/// <summary>
/// Issuing invoices in a supplier's name. Unlike everything else in this module, these are
/// <b>ours</b>: numbered, signed, chained and never edited.
/// </summary>
public class SelfBilledInvoiceServiceTests
{
    private readonly ISelfBilledInvoiceStorage _invoices = Substitute.For<ISelfBilledInvoiceStorage>();
    private readonly IGoodsReceiptStorage _receipts = Substitute.For<IGoodsReceiptStorage>();
    private readonly ISeriesStorage _series = Substitute.For<ISeriesStorage>();
    private readonly ISupplierPaymentStorage _payments = Substitute.For<ISupplierPaymentStorage>();
    private readonly IDocumentSigner _signer = Substitute.For<IDocumentSigner>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ITransaction _transaction = Substitute.For<ITransaction>();

    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _supplierId = Guid.NewGuid();
    private readonly Guid _warehouseId = Guid.NewGuid();

    private readonly List<SelfBilledInvoice> _stored = [];
    private readonly List<GoodsReceipt> _storedReceipts = [];

    private static readonly PurchaseOrderSupplierDto Supplier =
        new("F001", "Fornecedor Teste, Lda", "501234567");

    public SelfBilledInvoiceServiceTests()
    {
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(_transaction);

        _signer.Sign(
                Arg.Any<DateOnly>(), Arg.Any<DateTime>(), Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>())
            // Distinct per document number, so a broken chain shows up as a repeated hash. Padded
            // to the length of a real signature, because the QR code reads 4 characters out of it.
            .Returns(call => new DocumentSignature($"hash-{call.ArgAt<string>(2)}".PadRight(44, 'x'), "1"));

        _invoices.When(x => x.AddAsync(Arg.Any<SelfBilledInvoice>(), Arg.Any<CancellationToken>()))
            .Do(call => _stored.Add(call.Arg<SelfBilledInvoice>()));

        _invoices.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored.FirstOrDefault(x => x.Id == call.ArgAt<Guid>(0)));

        _invoices.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<SelfBilledInvoice>)[.. _stored]);

        // The chain, derived from what is stored the way the real storage derives it.
        _invoices.GetLastHashAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _stored
                .Where(x => x.SeriesId == call.ArgAt<Guid>(0))
                .OrderByDescending(x => x.SequenceNumber)
                .Select(x => x.Hash)
                .FirstOrDefault() ?? string.Empty);

        _invoices.GetSelfBilledQuantitiesAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var wanted = call.ArgAt<IReadOnlyCollection<Guid>>(0);

                return (IReadOnlyDictionary<Guid, decimal>)_stored
                    .Where(invoice => !invoice.IsVoided)
                    .SelectMany(invoice => invoice.Lines)
                    .Where(line => line.ReceiptLineId is not null && wanted.Contains(line.ReceiptLineId.Value))
                    .GroupBy(line => line.ReceiptLineId!.Value)
                    .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
            });

        _receipts.GetForUpdateByLineAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => _storedReceipts.FirstOrDefault(
                receipt => receipt.Lines.Any(line => line.Id == call.ArgAt<Guid>(0))));

        _receipts.GetAllAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyList<GoodsReceipt>)[.. _storedReceipts]);
    }

    private SelfBilledInvoiceService CreateService() =>
        new(_invoices,
            _receipts,
            _series,
            _payments,
            _signer,
            _unitOfWork,
            Options.Create(new FiscalOptions { IssuerTaxId = "500123456", CertificateNumber = "9999" }));

    /// <summary>A communicated series, marked for self-billing, which is the only kind that serves.</summary>
    private Series GivenSeries(bool selfBilling = true, bool communicated = true, string documentType = "FT")
    {
        var series = new Series
        {
            CompanyId = _companyId,
            DocumentType = documentType,
            SeriesCode = "AF2026",
            SelfBilling = selfBilling
        };

        if (communicated)
            series.Communicate("JFTX7RK9", DateTime.UtcNow);

        _series.GetForUpdateAsync(series.Id, Arg.Any<CancellationToken>()).Returns(series);

        return series;
    }

    private GoodsReceipt GivenReceipt(decimal quantity = 10m, decimal unitCost = 5m)
    {
        var receipt = GoodsReceipt.Create(
            _companyId,
            _supplierId,
            new SupplierSnapshot("F001", "Fornecedor Teste, Lda", "501234567"),
            $"REC2026/{_storedReceipts.Count + 1}",
            new DateOnly(2026, 3, 10),
            _warehouseId,
            [new GoodsReceiptLine
            {
                ProductCode = "ART001",
                ProductDescription = "Artigo de teste",
                Quantity = quantity,
                UnitCost = unitCost
            }]);

        _storedReceipts.Add(receipt);
        return receipt;
    }

    private IssueSelfBilledInvoiceRequest Request(
        Series series,
        params SelfBilledInvoiceLineRequest[] lines) =>
        new(_companyId,
            _supplierId,
            series.Id,
            new DateOnly(2026, 3, 15),
            Supplier,
            lines.Length == 0 ? [Line()] : lines,
            "Acordo 2026/01");

    private static SelfBilledInvoiceLineRequest Line(
        decimal quantity = 10m,
        decimal unitPrice = 5m,
        Guid? receiptLineId = null) =>
        new("ART001", "Artigo de teste", quantity, unitPrice, 23m, ReceiptLineId: receiptLineId);

    [Fact]
    public async Task IssueAsync_numbers_signs_and_stamps_the_document()
    {
        var series = GivenSeries();

        var result = await CreateService().IssueAsync(Request(series), "user-1");

        result.DocumentNumber.Should().Be("FT AF2026/1");
        result.Atcud.Should().Be("JFTX7RK9-1");
        _stored.Single().Hash.Should().StartWith("hash-FT AF2026/1");
        // Only the 4 printed characters of the signature leave the server.
        result.PrintableHash.Should().HaveLength(4);
        result.Status.Should().Be(DocumentStatuses.Normal);
        result.NetTotal.Should().Be(50m);
        result.TaxPayable.Should().Be(11.50m);
        result.GrossTotal.Should().Be(61.50m);
    }

    /// <summary>
    /// The QR code of a self-billed invoice is the mirror of a sale: we are the issuer, because the
    /// program that signed it is ours, and the supplier is the other party.
    /// </summary>
    [Fact]
    public async Task IssueAsync_puts_us_as_issuer_and_the_supplier_as_the_other_party_in_the_qr_code()
    {
        var series = GivenSeries();

        var result = await CreateService().IssueAsync(Request(series), "user-1");

        result.QrCodePayload.Should().Contain("A:500123456");
        result.QrCodePayload.Should().Contain("B:501234567");
        result.QrCodePayload.Should().Contain("R:9999");
    }

    [Fact]
    public async Task IssueAsync_chains_each_document_onto_the_previous_one_of_the_series()
    {
        var series = GivenSeries();
        var service = CreateService();

        await service.IssueAsync(Request(series), "user-1");
        await service.IssueAsync(Request(series), "user-1");

        _stored.Should().HaveCount(2);
        _stored[0].PreviousHash.Should().BeEmpty();
        _stored[1].PreviousHash.Should().Be(_stored[0].Hash);
        _stored[1].SequenceNumber.Should().Be(2);
    }

    /// <summary>
    /// The reason the flag exists. An ordinary sales series would put our own sales and the
    /// supplier's into one chain, and into one SAF-T file.
    /// </summary>
    [Fact]
    public async Task IssueAsync_refuses_a_series_that_is_not_for_self_billing()
    {
        var series = GivenSeries(selfBilling: false);

        var act = () => CreateService().IssueAsync(Request(series));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not a self-billing series*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_the_tax_authority_has_not_validated()
    {
        var series = GivenSeries(communicated: false);

        var act = () => CreateService().IssueAsync(Request(series));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*validation code*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_series_of_another_company()
    {
        var series = GivenSeries();
        var request = Request(series) with { CompanyId = Guid.NewGuid() };

        var act = () => CreateService().IssueAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not belong*");
    }

    [Fact]
    public async Task IssueAsync_bills_a_goods_receipt_line_and_records_where_it_came_from()
    {
        var series = GivenSeries();
        var receipt = GivenReceipt(quantity: 10m);
        var receiptLine = receipt.Lines.First();

        var result = await CreateService().IssueAsync(
            Request(series, Line(quantity: 4m, receiptLineId: receiptLine.Id)));

        var line = result.Lines.Should().ContainSingle().Subject;
        line.ReceiptLineId.Should().Be(receiptLine.Id);
        line.ReceiptId.Should().Be(receipt.Id);
    }

    [Fact]
    public async Task IssueAsync_refuses_to_bill_more_than_the_receipt_brought_in()
    {
        var series = GivenSeries();
        var receiptLine = GivenReceipt(quantity: 10m).Lines.First();
        var service = CreateService();

        await service.IssueAsync(Request(series, Line(quantity: 7m, receiptLineId: receiptLine.Id)));

        var act = () => service.IssueAsync(Request(series, Line(quantity: 4m, receiptLineId: receiptLine.Id)));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*left to bill*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_goods_receipt_of_another_supplier()
    {
        var series = GivenSeries();
        var receiptLine = GivenReceipt().Lines.First();

        var request = Request(series, Line(receiptLineId: receiptLine.Id)) with { SupplierId = Guid.NewGuid() };

        var act = () => CreateService().IssueAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*another supplier*");
    }

    [Fact]
    public async Task IssueAsync_refuses_an_exempt_line_with_no_reason()
    {
        var series = GivenSeries();
        var line = Line() with { TaxCode = TaxCodes.Exempt, TaxPercentage = 0m };

        var act = () => CreateService().IssueAsync(Request(series, line));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*exemption code*");
    }

    [Fact]
    public async Task IssueAsync_totals_the_tax_by_rate()
    {
        var series = GivenSeries();

        var result = await CreateService().IssueAsync(Request(
            series,
            Line(quantity: 10m, unitPrice: 5m),
            Line(quantity: 2m, unitPrice: 10m)));

        var summary = result.TaxSummary.Should().ContainSingle().Subject;
        summary.TaxPercentage.Should().Be(23m);
        summary.TaxableBase.Should().Be(70m);
        summary.TaxAmount.Should().Be(16.10m);
    }

    /// <summary>
    /// Acceptance is what article 36.º n.º 11 requires of each document, not merely a standing
    /// agreement, so it is recorded rather than assumed.
    /// </summary>
    [Fact]
    public async Task AcceptAsync_records_the_supplier_acceptance_once()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(series));

        issued.AcceptedBySupplierAtUtc.Should().BeNull();

        var accepted = await service.AcceptAsync(issued.Id);
        accepted!.AcceptedBySupplierAtUtc.Should().NotBeNull();

        var act = () => service.AcceptAsync(issued.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already accepted*");
    }

    [Fact]
    public async Task VoidAsync_appends_the_status_change_and_leaves_the_document_alone()
    {
        var series = GivenSeries();
        var service = CreateService();
        var issued = await service.IssueAsync(Request(series), "user-1");

        var result = await service.VoidAsync(issued.Id, "Erro de faturação", "user-2");

        result!.Status.Should().Be(DocumentStatuses.Voided);

        var document = _stored.Single();
        // The header keeps what it was issued with; only the appended change moved.
        document.Status.Should().Be(DocumentStatuses.Normal);
        document.StatusChanges.Should().ContainSingle().Which.Reason.Should().Be("Erro de faturação");

        await _invoices.Received(1).AddStatusChangeAsync(
            Arg.Any<SelfBilledInvoiceStatusChange>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VoidAsync_puts_the_receipt_line_back_on_the_shelf()
    {
        var series = GivenSeries();
        var receiptLine = GivenReceipt(quantity: 10m).Lines.First();
        var service = CreateService();

        var issued = await service.IssueAsync(Request(series, Line(quantity: 10m, receiptLineId: receiptLine.Id)));
        await service.VoidAsync(issued.Id, "Erro", "user-2");

        var pending = await service.GetUnbilledReceiptLinesAsync(_companyId);

        pending.Should().ContainSingle().Which.PendingQuantity.Should().Be(10m);
    }

    [Fact]
    public async Task GetUnbilledReceiptLinesAsync_reports_what_is_left_of_each_receipt_line()
    {
        var series = GivenSeries();
        var receiptLine = GivenReceipt(quantity: 10m).Lines.First();
        var service = CreateService();

        await service.IssueAsync(Request(series, Line(quantity: 6m, receiptLineId: receiptLine.Id)));

        var pending = await service.GetUnbilledReceiptLinesAsync(_companyId);

        var line = pending.Should().ContainSingle().Subject;
        line.BilledQuantity.Should().Be(6m);
        line.PendingQuantity.Should().Be(4m);
    }

    [Fact]
    public async Task IssueAsync_refuses_a_document_with_no_lines()
    {
        var series = GivenSeries();
        var request = Request(series) with { Lines = [] };

        var act = () => CreateService().IssueAsync(request);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*at least one line*");
    }
}
