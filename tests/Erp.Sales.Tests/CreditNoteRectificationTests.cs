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
        _context.DocumentStorage.GetByIdAsync(creditNote.Id, Arg.Any<CancellationToken>()).Returns(creditNote);

        var secondSeries = _context.GivenCommunicatedSeries(_companyId, "NC2027", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(secondSeries.Id, creditNote.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*itself a rectifying document*");
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
        _context.DocumentStorage.GetByIdAsync(foreignInvoice.Id, Arg.Any<CancellationToken>())
            .Returns(foreignInvoice);

        var creditSeries = _context.GivenCommunicatedSeries(_companyId, "NC2026", "NC");

        var act = () => _context.CreateService().IssueAsync(
            RectifyingRequest(creditSeries.Id, foreignInvoice.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*belongs to another company*");
    }
}
