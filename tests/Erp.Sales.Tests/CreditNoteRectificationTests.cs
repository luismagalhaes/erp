using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Erp.Sales.Tests;

/// <summary>
/// A credit or debit note corrects another document, and article 36.º n.º 5 of the CIVA requires
/// it to say which one and why. These tests hold both halves of that rule: a rectifying document
/// cannot be issued without the reference, and any other type cannot carry one.
/// </summary>
public class CreditNoteRectificationTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();

    private CreateInvoiceRequest RectifyingRequest(
        Guid seriesId,
        Guid? rectifiedDocumentId,
        string? reason = "Devolução de mercadoria") =>
        new(_companyId,
            seriesId,
            new DateOnly(2026, 1, 20),
            new CustomerRequest("500123456", "Cliente Teste", "Rua Um, Lisboa"),
            [SalesTestContext.Line()],
            rectifiedDocumentId,
            reason);

    /// <summary>Issues an invoice through the service, so it is a genuinely issued document.</summary>
    private async Task<InvoiceDetailDto> GivenIssuedInvoiceAsync()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, "A2026");

        var invoice = await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        var persisted = _context.Persisted[^1];
        _context.DocumentStorage.GetByIdAsync(persisted.Id, Arg.Any<CancellationToken>()).Returns(persisted);

        // The service reaches the document through the locking read, so that is what the tests
        // have to serve.
        _context.DocumentStorage.GetForUpdateAsync(persisted.Id, Arg.Any<CancellationToken>()).Returns(persisted);

        return invoice;
    }

    [Fact]
    public async Task IssueAsync_records_the_document_the_credit_note_corrects()
    {
        var invoice = await GivenIssuedInvoiceAsync();
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var creditNote = await _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id), "user-1");

        creditNote.RectifiedDocumentNumber.Should().Be(invoice.DocumentNumber);
        creditNote.RectificationReason.Should().Be("Devolução de mercadoria");

        var persisted = _context.Persisted[^1];
        persisted.RectifiedDocumentId.Should().Be(_context.Persisted[0].Id);
        persisted.IsRectifying.Should().BeTrue();
    }

    [Theory]
    [InlineData("NC")]
    [InlineData("ND")]
    public async Task IssueAsync_refuses_a_rectifying_document_without_a_reference(string documentType)
    {
        var series = _context.GivenCommunicatedSeries(_companyId, "R2026", documentType);

        var act = () => _context.CreateService().IssueAsync(RectifyingRequest(series.Id, null), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*must identify the document it corrects*");
    }

    [Fact]
    public async Task IssueAsync_refuses_a_rectifying_document_without_a_reason()
    {
        await GivenIssuedInvoiceAsync();
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id, reason: "   "), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*must state why*");
    }

    /// <summary>A reference on an invoice is meaningless, so it is refused rather than ignored.</summary>
    [Fact]
    public async Task IssueAsync_refuses_a_reference_on_a_document_that_corrects_nothing()
    {
        await GivenIssuedInvoiceAsync();
        var series = _context.GivenCommunicatedSeries(_companyId, "B2026");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(series.Id, _context.Persisted[0].Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*does not correct another document*");
    }

    [Fact]
    public async Task IssueAsync_refuses_to_correct_a_voided_document()
    {
        await GivenIssuedInvoiceAsync();
        _context.Persisted[0].Void("Erro", "user-1", DateTime.UtcNow);

        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*nothing to correct*");
    }

    [Fact]
    public async Task IssueAsync_refuses_to_correct_another_rectifying_document()
    {
        await GivenIssuedInvoiceAsync();
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        await _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id), "user-1");

        var creditNote = _context.Persisted[^1];
        _context.DocumentStorage.GetForUpdateAsync(creditNote.Id, Arg.Any<CancellationToken>()).Returns(creditNote);

        var secondSeries = _context.GivenCommunicatedSeries(_companyId, "NC2027", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(secondSeries.Id, creditNote.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*itself a rectifying document*");
    }

    /// <summary>
    /// The ceiling is only safe if the document row is locked before the credit already issued is
    /// read: otherwise two notes issued at the same time would both see the old amount. Reading
    /// without the lock is exactly the bug this guards against, so the order is asserted.
    /// </summary>
    [Fact]
    public async Task IssueAsync_locks_the_document_before_reading_what_is_already_credited()
    {
        await GivenIssuedInvoiceAsync();
        var invoice = _context.Persisted[0];
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        await _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, invoice.Id), "user-1");

        Received.InOrder(() =>
        {
            _context.DocumentStorage.GetForUpdateAsync(invoice.Id, Arg.Any<CancellationToken>());
            _context.DocumentStorage.GetCreditedAmountAsync(invoice.Id, Arg.Any<CancellationToken>());
        });
    }

    /// <summary>A document cannot be credited for more than it is worth.</summary>
    [Fact]
    public async Task IssueAsync_refuses_a_credit_note_larger_than_the_document()
    {
        await GivenIssuedInvoiceAsync();
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        // The invoice is worth 246.00; three units of the same line come to 369.00.
        var request = RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id) with
        {
            Lines = [SalesTestContext.Line(quantity: 3)]
        };

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot be credited for*");
    }

    [Fact]
    public async Task IssueAsync_allows_a_credit_note_up_to_the_full_value()
    {
        await GivenIssuedInvoiceAsync();
        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var creditNote = await _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, _context.Persisted[0].Id), "user-1");

        creditNote.GrossTotal.Should().Be(246m);
    }

    /// <summary>
    /// Credit notes already issued eat into what is left, so a second one has to fit the remainder.
    /// </summary>
    [Fact]
    public async Task IssueAsync_counts_the_credit_already_issued()
    {
        await GivenIssuedInvoiceAsync();
        var invoice = _context.Persisted[0];

        _context.DocumentStorage.GetCreditedAmountAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(200m);

        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, invoice.Id), "user-1");

        // 246.00 worth, 200.00 already credited, so only 46.00 is left.
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*already credited: only 46.00 can still be credited*");
    }

    [Fact]
    public async Task IssueAsync_allows_a_credit_note_that_fits_the_remainder()
    {
        await GivenIssuedInvoiceAsync();
        var invoice = _context.Persisted[0];

        _context.DocumentStorage.GetCreditedAmountAsync(invoice.Id, Arg.Any<CancellationToken>()).Returns(123m);

        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var request = RectifyingRequest(creditSeries.Id, invoice.Id) with
        {
            Lines = [SalesTestContext.Line(quantity: 1)]
        };

        var creditNote = await _context.CreateService().IssueAsync(request, "user-1");

        creditNote.GrossTotal.Should().Be(123m);
    }

    /// <summary>
    /// A debit note adds to what the customer owes, so it takes nothing from the original document
    /// and is not capped by its value.
    /// </summary>
    [Fact]
    public async Task IssueAsync_does_not_cap_a_debit_note()
    {
        await GivenIssuedInvoiceAsync();
        var debitSeries = _context.GivenCommunicatedSeries(_companyId, "ND2026", "ND");

        var request = RectifyingRequest(debitSeries.Id, _context.Persisted[0].Id) with
        {
            Lines = [SalesTestContext.Line(quantity: 5)]
        };

        var debitNote = await _context.CreateService().IssueAsync(request, "user-1");

        debitNote.GrossTotal.Should().Be(615m);
    }

    [Fact]
    public async Task IssueAsync_refuses_to_correct_a_document_of_another_company()
    {
        // An invoice issued by a different company, reachable by id but not ours to correct.
        var otherCompanyId = Guid.NewGuid();
        var otherSeries = _context.GivenCommunicatedSeries(otherCompanyId, "X2026");

        await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(otherCompanyId, otherSeries.Id), "user-2");

        var foreignInvoice = _context.Persisted[^1];
        _context.DocumentStorage.GetForUpdateAsync(foreignInvoice.Id, Arg.Any<CancellationToken>())
            .Returns(foreignInvoice);

        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, foreignInvoice.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*belongs to another company*");
    }
}
