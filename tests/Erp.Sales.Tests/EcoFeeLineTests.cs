using Erp.Sales.Infrastructure.Contracts;
using FluentAssertions;

namespace Erp.Sales.Tests;

/// <summary>
/// An eco-fee ("Ecovalor") line references the article line it was generated for by position, since
/// neither side has a database id yet when the request is built.
/// </summary>
public class EcoFeeLineTests
{
    private readonly SalesTestContext _context = new();
    private readonly Guid _companyId = Guid.NewGuid();

    [Fact]
    public async Task IssueAsync_links_the_eco_fee_line_to_the_line_it_was_generated_for()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id,
            SalesTestContext.Line(),
            SalesTestContext.Line() with
            {
                ProductCode = "ECOVALOR-BAT",
                ProductDescription = "Ecovalor - Pilhas e baterias",
                IsEcoFee = true,
                EcoFeeForLineNumber = 1
            });

        await _context.CreateService().IssueAsync(request, "user-1");

        var document = _context.Persisted.Single();
        var article = document.Lines.Single(x => x.LineNumber == 1);
        var ecoFee = document.Lines.Single(x => x.LineNumber == 2);

        ecoFee.IsEcoFee.Should().BeTrue();
        ecoFee.EcoFeeForLineId.Should().Be(article.Id);
        article.IsEcoFee.Should().BeFalse();
        article.EcoFeeForLineId.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_refuses_an_eco_fee_line_with_no_parent()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id,
            SalesTestContext.Line() with { IsEcoFee = true });

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*must say which line it belongs to*");
    }

    [Fact]
    public async Task IssueAsync_refuses_an_eco_fee_line_referencing_a_line_that_does_not_exist()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id,
            SalesTestContext.Line() with { IsEcoFee = true, EcoFeeForLineNumber = 99 });

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*which was not found*");
    }

    /// <summary>The fee is collected in full for the managing entity, so a commercial discount on the
    /// article it rides along with can never carry over to it.</summary>
    [Fact]
    public async Task IssueAsync_refuses_a_discount_on_an_eco_fee_line()
    {
        var series = _context.GivenCommunicatedSeries(_companyId);

        var request = SalesTestContext.InvoiceRequest(_companyId, series.Id,
            SalesTestContext.Line(),
            SalesTestContext.Line() with
            {
                ProductCode = "ECOVALOR-BAT",
                ProductDescription = "Ecovalor - Pilhas e baterias",
                IsEcoFee = true,
                EcoFeeForLineNumber = 1,
                DiscountPercentage = 10
            });

        var act = () => _context.CreateService().IssueAsync(request, "user-1");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot carry a discount*");
    }
}
