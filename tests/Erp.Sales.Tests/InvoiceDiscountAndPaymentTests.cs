using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;

namespace Erp.Sales.Tests;

/// <summary>
/// Line discounts, due dates and the payment a fatura-recibo carries.
/// </summary>
public class InvoiceDiscountAndPaymentTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();

    private static readonly DateOnly DocumentDate = new(2026, 1, 15);

    [Fact]
    public async Task A_line_discount_lowers_the_taxable_amount_and_is_kept_apart()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        // 3 × 33.33 = 99.99; 10% off = 10.00 (rounded); taxable 89.99; VAT 23% = 20.70.
        var issued = await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(quantity: 3, unitPrice: 33.33m) with { DiscountPercentage = 10m }),
            "user-1");

        var line = issued.Lines.Single();
        line.UnitPrice.Should().Be(33.33m);
        line.DiscountAmount.Should().Be(10.00m);
        line.LineAmount.Should().Be(89.99m);
        line.TaxAmount.Should().Be(20.70m);

        issued.GrossLinesTotal.Should().Be(99.99m);
        issued.DiscountTotal.Should().Be(10.00m);
        issued.NetTotal.Should().Be(89.99m);
        issued.TaxPayable.Should().Be(20.70m);
        issued.GrossTotal.Should().Be(110.69m);
    }

    /// <summary>SAF-T wants the price after the discount, so quantity times price gives the amount.</summary>
    [Fact]
    public async Task The_net_unit_price_is_what_the_line_is_worth_per_unit()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        await _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(
                _companyId,
                series.Id,
                SalesTestContext.Line(quantity: 4, unitPrice: 25m) with { DiscountPercentage = 20m }),
            "user-1");

        _context.Persisted.Single().Lines.Single().NetUnitPrice.Should().Be(20m);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task A_discount_outside_0_to_100_is_refused(decimal discount)
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var act = () => _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id, SalesTestContext.Line() with { DiscountPercentage = discount }),
            "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*discount*");
    }

    [Fact]
    public async Task The_due_date_is_kept_and_customer_address_is_complete()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with
        {
            DueDate = DocumentDate.AddDays(30),
            Customer = new CustomerRequest("500123456", "Cliente Teste", "Rua Um", PostalCode: "1000-001", City: "Lisboa")
        };

        var issued = await _context.CreateService().IssueAsync(request, "user-1");

        issued.DueDate.Should().Be(DocumentDate.AddDays(30));
        issued.CustomerPostalCode.Should().Be("1000-001");
        issued.CustomerCity.Should().Be("Lisboa");
    }

    [Fact]
    public async Task A_due_date_before_the_document_is_refused()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with { DueDate = DocumentDate.AddDays(-1) };

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*due date*");
    }

    [Fact]
    public async Task A_fatura_recibo_without_payment_methods_is_refused()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, documentType: "FR");

        var act = () => _context.CreateService().IssueAsync(
            SalesTestContext.InvoiceRequest(_companyId, series.Id), "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*how the money was received*");
    }

    [Fact]
    public async Task A_fatura_recibo_records_how_it_was_paid_and_is_due_the_same_day()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, documentType: "FR");

        // Default line: 2 × 100 + 23% = 246.00.
        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with
        {
            DueDate = DocumentDate.AddDays(30),
            Payments =
            [
                new CreatePaymentMethodRequest("NU", 46m, DocumentDate),
                new CreatePaymentMethodRequest("CC", 200m, DocumentDate)
            ]
        };

        var issued = await _context.CreateService().IssueAsync(request, "user-1");

        issued.Payments.Should().HaveCount(2);
        issued.Payments!.Sum(payment => payment.Amount).Should().Be(246m);
        issued.DueDate.Should().Be(DocumentDate);
    }

    [Fact]
    public async Task A_fatura_recibo_paid_short_is_refused()
    {
        var series = _context.GivenCommunicatedSeries(_companyId, documentType: "FR");
        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with
        {
            Payments = [new CreatePaymentMethodRequest("NU", 200m, DocumentDate)]
        };

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*add up to*");
    }

    [Fact]
    public async Task An_invoice_refuses_payment_methods()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);
        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id) with
        {
            Payments = [new CreatePaymentMethodRequest("NU", 246m, DocumentDate)]
        };

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*not paid when issued*");
    }
}
